#!/usr/bin/env python3
"""Build a source-independent consumer and verify transitive runtime assets."""
import json
import subprocess
import sys
import tempfile
from pathlib import Path

root=Path(__file__).resolve().parent.parent
version=json.loads((root/'csweet-plugin.json').read_text())['version']
feed=Path(sys.argv[1]).resolve()
with tempfile.TemporaryDirectory(prefix='csweet-openhands-package-consumer-') as temporary:
    project=Path(temporary)/'Consumer.csproj'
    project.write_text(f'''<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>
<ItemGroup><PackageReference Include="CSweet.Agent.SoftwareDeveloper.OpenHands" Version="{version}" /></ItemGroup>
</Project>''',encoding='utf-8')
    (project.parent/'Program.cs').write_text('''using CSweet.Agent.SoftwareDeveloper.OpenHands;
var agent=new OpenHandsDeveloperAgent();
foreach(var name in new[]{"worker/worker.py","worker/requirements-linux.lock","worker/build-requirements.lock","csweet-plugin.json"})
    if(!File.Exists(Path.Combine(AppContext.BaseDirectory,name))) throw new InvalidOperationException("Missing packaged runtime asset: "+name);
Console.WriteLine("Independent package consumer loaded "+agent.AgentId+" "+agent.Version);
''',encoding='utf-8')
    subprocess.run(['dotnet','restore',str(project),'--source',str(feed),'--source','https://api.nuget.org/v3/index.json'],check=True)
    subprocess.run(['dotnet','run','--project',str(project),'-c','Release','--no-restore'],check=True)
