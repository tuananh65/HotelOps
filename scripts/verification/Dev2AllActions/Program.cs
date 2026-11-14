try
{
    if(args.Length>=2&&args[0]=="offline")await Offline.Run(Path.GetFullPath(args[1]));
    else if(args.Length==4&&args[0]=="recover")await OrphanRecovery.Run(Path.GetFullPath(args[1]),Path.GetFullPath(args[2]),args[3]);
    else if(args.Length==3&&args[0]=="host")await HostAcceptance.Run(Path.GetFullPath(args[1]),args[2]);
    else if(args.Length==3&&args[0]=="normal")await HostAcceptance.Run(Path.GetFullPath(args[1]),args[2],normalAuthority:true);
    else if(args.Length==3&&args[0]=="browser")await BrowserAcceptance.Run(Path.GetFullPath(args[1]),args[2]);
    else if(args.Length==3&&args[0]=="focused")await HostAcceptance.Run(Path.GetFullPath(args[1]),args[2],focused:true);
    else if(args.Length==4&&args[0]=="focused")await HostAcceptance.Run(Path.GetFullPath(args[1]),args[2],focused:true,focusAction:args[3]);
    else if(args.Length==2&&args[0]=="fixture-smoke")await HostAcceptance.Run(Path.GetFullPath(args[1]),null,fixtureOnly:true);
    else throw new InvalidOperationException("Use offline, fixture-smoke or host with an explicit receipt directory");
}
catch(Exception error) when(args.Length>=2&&args[0] is "host" or "normal" or "browser" or "focused" or "fixture-smoke")
{
    var output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);
    var diagnostic=FailureDiagnostics.Capture(error,"harness-precondition-or-unhandled",null,null,null,output);
    var json=System.Text.Json.JsonSerializer.Serialize(diagnostic,new System.Text.Json.JsonSerializerOptions{WriteIndented=true});
    await File.WriteAllTextAsync(Path.Combine(output,"FIXTURE_FAILURE_DIAGNOSTICS.json"),json);
    if(args[0]=="fixture-smoke")await File.WriteAllTextAsync(Path.Combine(output,"FIXTURE_SMOKE_RESULT.json"),"{\"status\":\"FAIL\",\"phase\":\"precondition-or-unhandled\",\"cleanup\":\"NOT_VERIFIED\"}");
    Console.Error.WriteLine(json);Environment.ExitCode=1;
}
