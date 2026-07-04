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
            let! body = response.Content.ReadAsStringAsync(cancellationToken)

            return
                { StatusCode = int response.StatusCode
                  IsSuccess = response.IsSuccessStatusCode
                  Body = body }
        }
