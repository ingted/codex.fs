namespace CodexFs

open System
open System.Diagnostics
open System.Text
open System.Threading
open System.Threading.Tasks

/// Guarded operating-system process execution for headless engine runs.
module ProcessRunner =

    /// Command data passed to the process runner.
    type ProcessCommand =
        { /// Executable file name or absolute path.
          FileName: string
          /// Argument list passed without shell interpolation.
          Arguments: string list
          /// Optional working directory.
          WorkingDirectory: string option
          /// Environment variable overlay; `None` means remove the variable.
          Environment: Map<string, string option> }

    /// Guard options for one process run.
    type ProcessRunOptions =
        { /// Maximum time allowed before the process is killed.
          Timeout: TimeSpan
          /// Grace period to wait after sending kill to the process tree.
          KillGracePeriod: TimeSpan }

    /// Terminal process runner outcome.
    type ProcessOutcome =
        /// Process exited before timeout or cancellation.
        | Exited
        /// Process exceeded the configured timeout and was killed.
        | TimedOut
        /// Caller cancellation was observed and the process was killed.
        | Cancelled
        /// Process could not be started or guarded.
        | StartFailed of string

    /// Captured process execution result.
    type ProcessRunResult =
        { /// Terminal outcome.
          Outcome: ProcessOutcome
          /// Process exit code, when the process started and exited.
          ExitCode: int option
          /// UTC start timestamp.
          StartedUtc: DateTimeOffset
          /// UTC terminal timestamp.
          CompletedUtc: DateTimeOffset
          /// Captured stdout text.
          Stdout: string
          /// Captured stderr text.
          Stderr: string }

    /// Lease recorded when codex.fs owns a spawned process and may recover it later.
    type ProcessLease =
        { /// Operating system process id.
          ProcessId: int
          /// Expected process name captured at start time.
          ProcessName: string
          /// UTC start timestamp captured from the process when possible.
          StartedUtc: DateTimeOffset
          /// Non-secret codex.fs marker stored with the lease.
          Marker: string }

    /// Guard options for orphan process recovery.
    type OrphanRecoveryOptions =
        { /// Maximum allowed clock drift between lease start time and observed process start time.
          StartTimeTolerance: TimeSpan
          /// Grace period to wait after killing the process tree.
          KillGracePeriod: TimeSpan }

    /// Outcome of one orphan recovery attempt.
    type OrphanRecoveryOutcome =
        /// No process currently exists for the leased pid.
        | NotRunning
        /// The pid exists but process name or start time did not match the lease.
        | LeaseMismatch
        /// The leased process was killed.
        | Terminated
        /// The leased process matched but could not be killed.
        | TerminationFailed
        /// Recovery could not inspect or control the process.
        | RecoveryFailed of string

    /// Result of one orphan recovery attempt.
    type OrphanRecoveryResult =
        { /// Lease used for the recovery attempt.
          Lease: ProcessLease
          /// Recovery outcome.
          Outcome: OrphanRecoveryOutcome
          /// True when a process was found for the leased pid.
          WasRunning: bool
          /// True when process identity matched the lease.
          WasMatched: bool
          /// True when recovery terminated the leased process.
          WasTerminated: bool
          /// Non-secret error text, when available.
          Error: string option }

    /// Default conservative orphan recovery options.
    let defaultOrphanRecoveryOptions =
        { StartTimeTolerance = TimeSpan.FromSeconds 5.0
          KillGracePeriod = TimeSpan.FromSeconds 5.0 }

    /// Build a ProcessStartInfo from normalized command data.
    let startInfo (command: ProcessCommand) =
        let psi = ProcessStartInfo()
        psi.FileName <- command.FileName
        psi.UseShellExecute <- false
        psi.RedirectStandardOutput <- true
        psi.RedirectStandardError <- true
        psi.CreateNoWindow <- true
        // Explicit UTF-8 decoding prevents mojibake on Windows systems
        // where the default codepage (e.g. Big5/Windows-950) corrupts
        // non-ASCII stdout/stderr from child engine processes.
        psi.StandardOutputEncoding <- UTF8Encoding(false)
        psi.StandardErrorEncoding <- UTF8Encoding(false)

        command.Arguments
        |> List.iter (fun argument -> psi.ArgumentList.Add argument)

        command.WorkingDirectory
        |> Option.iter (fun directory -> psi.WorkingDirectory <- directory)

        command.Environment
        |> Map.iter (fun name value ->
            match value with
            | Some text -> psi.Environment[name] <- text
            | None ->
                if psi.Environment.ContainsKey name then
                    psi.Environment.Remove name |> ignore)

        psi

    /// Kill a process tree and wait briefly for the operating system to release it.
    let killProcessTree (proc: Process) (gracePeriod: TimeSpan) =
        task {
            try
                if not proc.HasExited then
                    proc.Kill(entireProcessTree = true)

                if not proc.HasExited then
                    use cts = new CancellationTokenSource(gracePeriod)

                    try
                        do! proc.WaitForExitAsync(cts.Token)
                    with
                    | :? OperationCanceledException -> ()
            with
            | _ -> ()
        }

    /// Convert a process start time to UTC.
    let processStartUtc (proc: Process) =
        DateTimeOffset(proc.StartTime.ToUniversalTime(), TimeSpan.Zero)

    /// Return true when the observed process identity matches the saved lease.
    let leaseMatchesProcess (options: OrphanRecoveryOptions) (lease: ProcessLease) (proc: Process) =
        let nameComparison =
            if OperatingSystem.IsWindows() then
                StringComparison.OrdinalIgnoreCase
            else
                StringComparison.Ordinal

        let nameMatches = String.Equals(proc.ProcessName, lease.ProcessName, nameComparison)
        let observedStartUtc = processStartUtc proc
        let delta = (observedStartUtc - lease.StartedUtc).Duration()
        nameMatches && delta <= options.StartTimeTolerance

    /// Recover one codex.fs-owned leased process if the pid still matches the recorded identity.
    let recoverLeasedProcessAsync (options: OrphanRecoveryOptions) (lease: ProcessLease) =
        task {
            try
                use proc = Process.GetProcessById lease.ProcessId

                if proc.HasExited then
                    return
                        { Lease = lease
                          Outcome = NotRunning
                          WasRunning = false
                          WasMatched = false
                          WasTerminated = false
                          Error = None }
                else
                    let matched =
                        try
                            leaseMatchesProcess options lease proc
                        with ex ->
                            false

                    if not matched then
                        return
                            { Lease = lease
                              Outcome = LeaseMismatch
                              WasRunning = true
                              WasMatched = false
                              WasTerminated = false
                              Error = None }
                    else
                        do! killProcessTree proc options.KillGracePeriod
                        proc.Refresh()

                        if proc.HasExited then
                            return
                                { Lease = lease
                                  Outcome = Terminated
                                  WasRunning = true
                                  WasMatched = true
                                  WasTerminated = true
                                  Error = None }
                        else
                            return
                                { Lease = lease
                                  Outcome = TerminationFailed
                                  WasRunning = true
                                  WasMatched = true
                                  WasTerminated = false
                                  Error = Some "Process did not exit after kill grace period." }
            with
            | :? ArgumentException ->
                return
                    { Lease = lease
                      Outcome = NotRunning
                      WasRunning = false
                      WasMatched = false
                      WasTerminated = false
                      Error = None }
            | ex ->
                return
                    { Lease = lease
                      Outcome = RecoveryFailed ex.Message
                      WasRunning = false
                      WasMatched = false
                      WasTerminated = false
                      Error = Some ex.Message }
        }

    /// Await a text capture task without allowing guarded termination to hang forever.
    let awaitTextWithin (captureTask: Task<string>) (timeout: TimeSpan) =
        task {
            if captureTask.IsCompleted then
                return captureTask.Result
            else
                let milliseconds =
                    if timeout <= TimeSpan.Zero then
                        0
                    elif timeout.TotalMilliseconds >= float Int32.MaxValue then
                        Int32.MaxValue
                    else
                        int timeout.TotalMilliseconds

                use delayCts = new CancellationTokenSource()
                let delayTask = Task.Delay(milliseconds, delayCts.Token)
                let! completed = Task.WhenAny(captureTask :> Task, delayTask)

                if obj.ReferenceEquals(completed, captureTask) then
                    delayCts.Cancel()
                    return captureTask.Result
                else
                    return String.Empty
        }

    /// Run a process, capture stdout/stderr and enforce timeout/cancellation by killing the process tree.
    let runAsync (options: ProcessRunOptions) (command: ProcessCommand) (cancellationToken: CancellationToken) =
        task {
            let startedUtc = DateTimeOffset.UtcNow

            try
                use proc = new Process()
                proc.StartInfo <- startInfo command
                proc.EnableRaisingEvents <- true

                if not (proc.Start()) then
                    return
                        { Outcome = StartFailed "Process.Start returned false."
                          ExitCode = None
                          StartedUtc = startedUtc
                          CompletedUtc = DateTimeOffset.UtcNow
                          Stdout = String.Empty
                          Stderr = String.Empty }
                else
                    let stdoutTask = proc.StandardOutput.ReadToEndAsync()
                    let stderrTask = proc.StandardError.ReadToEndAsync()

                    use timeoutCts = new CancellationTokenSource(options.Timeout)
                    use linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token)

                    try
                        do! proc.WaitForExitAsync(linkedCts.Token)
                        let! stdout = stdoutTask
                        let! stderr = stderrTask

                        return
                            { Outcome = Exited
                              ExitCode = Some proc.ExitCode
                              StartedUtc = startedUtc
                              CompletedUtc = DateTimeOffset.UtcNow
                              Stdout = stdout
                              Stderr = stderr }
                    with
                    | :? OperationCanceledException ->
                        let outcome =
                            if cancellationToken.IsCancellationRequested then
                                Cancelled
                            else
                                TimedOut

                        do! killProcessTree proc options.KillGracePeriod
                        let! stdout = awaitTextWithin stdoutTask options.KillGracePeriod
                        let! stderr = awaitTextWithin stderrTask options.KillGracePeriod

                        let exitCode =
                            if proc.HasExited then
                                Some proc.ExitCode
                            else
                                None

                        return
                            { Outcome = outcome
                              ExitCode = exitCode
                              StartedUtc = startedUtc
                              CompletedUtc = DateTimeOffset.UtcNow
                              Stdout = stdout
                              Stderr = stderr }
            with ex ->
                return
                    { Outcome = StartFailed ex.Message
                      ExitCode = None
                      StartedUtc = startedUtc
                      CompletedUtc = DateTimeOffset.UtcNow
                      Stdout = String.Empty
                      Stderr = String.Empty }
        }
