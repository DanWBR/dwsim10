# ApiSurfaceDiff

Lists the public types and members of two builds of the same assembly and prints what the
second one is missing. Run it after porting a project, against the .NET Framework build in
the DWSIM 10 tree and the .NET build here:

    dotnet run --project tools/ApiSurfaceDiff -- <old.dll> <new.dll> [probe folders...]

A non-zero exit means the port dropped something public. Members are compared by name, not
by signature: resolving signatures would need every referenced assembly of both builds on
disk, and a lost member is what this is looking for.
