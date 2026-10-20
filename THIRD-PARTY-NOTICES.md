# Third-party notices

OpenHands Software Agent SDK and OpenHands tools 1.56.0 are MIT-licensed upstream projects:
https://github.com/OpenHands/software-agent-sdk. The integration uses public extension points and
does not fork or redistribute a modified upstream SDK. Its Python environment is installed from
the exact versions and SHA-256 hashes in the worker lock files; upstream license files remain in
the installed distributions.

The guarded shell component is Microsoft.Agents.AI.Tools.Shell 1.15.0-preview.260722.1
(MIT, Microsoft Agent Framework). It is a subprocess policy component; Microsoft.Agents.AI.Harness
is not part of the execution engine.

The initial workflow baseline derives from CrosswiredStudios/CSweet.Agent.SoftwareDeveloper 2.1.1
under the repository owner's authorization. The original developer repositories are unchanged.
This new repository and its adapter code use the root MIT license.

SearXNG is independently deployed by C-Sweet and accessed over its platform research API.
It is not bundled into these agent packages. Its own license and distribution notices remain
with C-Sweet's SearXNG deployment.
