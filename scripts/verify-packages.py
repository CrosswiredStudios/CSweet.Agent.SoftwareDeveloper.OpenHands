#!/usr/bin/env python3
"""Verify manifest, independent NuGet package metadata and shipped worker files."""
import json
import sys
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

root=Path(__file__).resolve().parent.parent
version=json.loads((root/'csweet-plugin.json').read_text())['version']
feed=Path(sys.argv[1])
for name in ('CSweet.Agent.Development','CSweet.Agent.OpenHands','CSweet.Agent.SoftwareDeveloper.OpenHands'):
    path=feed/f'{name}.{version}.nupkg'
    with zipfile.ZipFile(path) as package:
        metadata=ET.fromstring(package.read(next(x for x in package.namelist() if x.endswith('.nuspec'))))
        ns={'n':metadata.tag.split('}')[0].strip('{')}
        assert metadata.find('.//n:id',ns).text==name, path
        assert metadata.find('.//n:version',ns).text==version,path
        if name=='CSweet.Agent.OpenHands':
            assert 'buildTransitive/CSweet.Agent.OpenHands.targets' in package.namelist(),'Transitive worker delivery missing'
            assert any(x.endswith('/worker.py') for x in package.namelist()),'Worker missing'
            assert any(x.endswith('/requirements-linux.lock') for x in package.namelist()),'Dependency lock missing'
            assert any(x.endswith('/build-requirements.lock') for x in package.namelist()),'Build lock missing'
    print(f'Verified {path.name}')
notes=root/'releases'/f'{version}.md'
assert notes.read_text().splitlines()[0]==f'# OpenHands Developer {version}'
