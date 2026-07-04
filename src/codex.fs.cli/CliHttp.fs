namespace CodexFs.Cli

open System
open System.Net.Http
open System.Net.Http.Json
open System.Threading
open System.Threading.Tasks
open CodexFs.Host

/// HTTP client helpers used by codex.fs.cli commands.
module CliHttp =

    /// Raw HTTP result returned by a CLI command client.
    type CliHttpResult =
        { /// HTTP status code as an integer.
          StatusCode: int
          /// True when the status code is in the 2xx range.
          IsSuccess: bool
          /// Response body text.
          Body: string }

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

    /// Send one prompt through the host control endpoint into PTCS MessageFabric.
    let sendSessionMessageAsync (client: HttpClient) (cancellationToken: CancellationToken) (options: Cli.SessionSendOptions) =
        task {
            let request: HostControl.SessionSendRequest =
                { Prompt = options.Prompt
                  FromParticipantId = "user.codexfs.cli"
                  Tags = [ "codex.fs"; "cli"; "session-send" ]
                  CorrelationId = String.Empty }

            use content = JsonContent.Create request
            let! response = client.PostAsync(sessionSendUri options.Host options.SessionId, content, cancellationToken)
            return! responseTextAsync response cancellationToken
        }

    /// Get current session inbox status through the host control endpoint.
    let getSessionStatusAsync (client: HttpClient) (cancellationToken: CancellationToken) (options: Cli.SessionTargetOptions) =
        task {
            let! response = client.GetAsync(sessionStatusUri options.Host options.SessionId, cancellationToken)
            return! responseTextAsync response cancellationToken
        }

    /// Get host health/status through the host control endpoint.
    let getHostStatusAsync (client: HttpClient) (cancellationToken: CancellationToken) (options: Cli.HostStatusOptions) =
        task {
            let! response = client.GetAsync(hostStatusUri options.Host, cancellationToken)
            return! responseTextAsync response cancellationToken
        }

    /// Bounded attach to session inbox through the host control endpoint.
    let attachSessionAsync (client: HttpClient) (cancellationToken: CancellationToken) (options: Cli.SessionTargetOptions) =
        task {
            let! response = client.PostAsync(sessionAttachUri options.Host options.SessionId, null, cancellationToken)
            return! responseTextAsync response cancellationToken
        }

    /// Drain current session inbox through the host control endpoint.
    let drainSessionAsync (client: HttpClient) (cancellationToken: CancellationToken) (options: Cli.SessionTargetOptions) =
        task {
            let! response = client.PostAsync(sessionDrainUri options.Host options.SessionId, null, cancellationToken)
            return! responseTextAsync response cancellationToken
        }
