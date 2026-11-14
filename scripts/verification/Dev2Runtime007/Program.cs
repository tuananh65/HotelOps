try {
 if(args.Length!=3 || args[0] is not ("acceptance" or "interactive")) throw new InvalidOperationException("Explicit007 mode, output and local HTTPS frontend required");
 await Runtime007.Run(Path.GetFullPath(args[1]),args[2],args[0]=="interactive");
} catch(Exception e) {
 if(args.Length>=2){var output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);await FixtureRunSafety.Write(Path.Combine(output,"RUNTIME007_FAILURE.json"),new {status="FAIL",exceptionType=e.GetType().Name,rawValuesRecorded=false});}
 Environment.ExitCode=1;
}
