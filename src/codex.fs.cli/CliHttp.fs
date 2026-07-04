namespace CodexFs.Cli

open System
open System.Net.Http
open System.Net.Http.Json
open System.Threading
open System.Threading.Tasks
open CodexFs.Host

/// HTTP client helpers used by the `codex.fs.cli` terminal command and `codex.fs` alias.
module CliHttp =

    /// Raw HTTP result returned by a CLI command client.
    type CliHttpResult =
        { /// HTTP status code as an integer.
          StatusCode: int
          /// True when the status code is in the 2xx range.
          IsSuccess: bool
          /// Response body text.
          Body: string }

    let transportFailure uri (ex: exn) =
        let message =
            [ $"codex.fs could not reach host endpoint: {uri}"
              ex.Message
              "Check that codex.fs.host is running and --host uses the advertised host URI, for example http://10.28.112.93:10481. Do not use the process PID as the port." ]
            |> String.concat Environment.NewLine

        { StatusCode = 0
          IsSuccess = false
          Body = message }

    /// Build the host endpoint URI for `session send`.
    let sessionSendUri (hostUri: string) (sessionId: string) =
        let baseUri = hostUri.TrimEnd('/')
        let escapedSessionId = Uri.EscapeDataString sessionId
        $"{baseUri}/api/codexfs/session/{escapedSessionId}/messages"

    /// Build the host endpoint URI for `host status`.
    let hostStatusUri (hostUri: string) =
        let baseUri = hostUri.TrimEnd('/')
        $"{baseUri}/api/codexfs/host/health"

    /// Build the host endpoint URI for `session status`.
    let sessionStatusUri (hostUri: string) (sessionId: string) =
        let baseUri = hostUri.TrimEnd('/')
        let escapedSessionId = Uri.EscapeDataString sessionId
        $"{baseUri}/api/codexfs/session/{escapedSessionId}/status"

    /// Build the host endpoint URI for `session attach`.
    let sessionAttachUri (hostUri: string) (sessionId: string) =
        let baseUri = hostUri.TrimEnd('/')
        let escapedSessionId = Uri.EscapeDataString sessionId
        $"{baseUri}/api/codexfs/session/{escapedSessionId}/attach"

    /// Build the host endpoint URI for `session drain`.
    let sessionDrainUri (hostUri: string) (sessionId: string) =
        let baseUri = hostUri.TrimEnd('/')
        let escapedSessionId = Uri.EscapeDataString sessionId
        $"{baseUri}/api/codexfs/session/{escapedSessionId}/drain"

    let responseTextAsync (response: HttpResponseMessage) (cancellationToken: CancellationToken) =
        task {
            let! body = response.Content.ReadAsStringAsync(cancellationToken)

            return
                { StatusCode = int response.StatusCode
                  IsSuccess = response.IsSuccessStatusCode
                  Body = body }
        }

    let getAsync (client: HttpClient) (cancellationToken: CancellationToken) (uri: string) =
        task {
            try
                let! response = client.GetAsync(uri, cancellationToken)
                return! responseTextAsync response cancellationToken
            with
            | :? HttpRequestException as ex -> return transportFailure uri ex
            | :? TaskCanceledException as ex -> return transportFailure uri ex
            | :? InvalidOperationException as ex -> return transportFailure uri ex
        }

    let postJsonAsync (client: HttpClient) (cancellationToken: CancellationToken) (uri: string) request =
        task {
            try
                use content = JsonContent.Create request
                let! response = client.PostAsync(uri, content, cancellationToken)
                return! responseTextAsync response cancellationToken
            with
            | :? HttpRequestException as ex -> return transportFailure uri ex
            | :? TaskCanceledException as ex -> return transportFailure uri ex
            | :? InvalidOperationException as ex -> return transportFailure uri ex
        }

    let postEmptyAsync (client: HttpClient) (cancellationToken: CancellationToken) (uri: string) =
        task {
            try
                let! response = client.PostAsync(uri, Unchecked.defaultof<HttpContent>, cancellationToken)
                return! responseTextAsync response cancellationToken
            with
            | :? HttpRequestException as ex -> return transportFailure uri ex
            | :? TaskCanceledException as ex -> return transportFailure uri ex
            | :? InvalidOperationException as ex -> return transportFailure uri ex
        }

    /// Send one prompt through the host control endpoint into PTCS MessageFabric.
    let sendSessionMessageAsync (client: HttpClient) (cancellationToken: CancellationToken) (options: Cli.SessionSendOptions) =
        task {
            let request: HostControl.SessionSendRequest =
                { Prompt = options.Prompt
                  FromParticipantId = "user.codexfs.cli"
                  WorkerId = options.WorkerId |> Option.defaultValue String.Empty
                  Tags = [ "codex.fs"; "cli"; "session-send" ]
                  CorrelationId = String.Empty }

            return! postJsonAsync client cancellationToken (sessionSendUri options.Host options.SessionId) request
        }

    /// Get current session inbox status through the host control endpoint.
    let getSessionStatusAsync (client: HttpClient) (cancellationToken: CancellationToken) (options: Cli.SessionTargetOptions) =
        getAsync client cancellationToken (sessionStatusUri options.Host options.SessionId)

    /// Get host health/status through the host control endpoint.
    let getHostStatusAsync (client: HttpClient) (cancellationToken: CancellationToken) (options: Cli.HostStatusOptions) =
        getAsync client cancellationToken (hostStatusUri options.Host)

    /// Bounded attach to session inbox through the host control endpoint.
    let attachSessionAsync (client: HttpClient) (cancellationToken: CancellationToken) (options: Cli.SessionTargetOptions) =
        postEmptyAsync client cancellationToken (sessionAttachUri options.Host options.SessionId)

    /// Drain current session inbox through the host control endpoint.
    let drainSessionAsync (client: HttpClient) (cancellationToken: CancellationToken) (options: Cli.SessionTargetOptions) =
        postEmptyAsync client cancellationToken (sessionDrainUri options.Host options.SessionId)
