using System.Runtime.CompilerServices;

// Project.Match writes the raid lifecycle; the AI test assemblies drive it in tests.
[assembly: InternalsVisibleTo("Project.Match")]
[assembly: InternalsVisibleTo("Project.AI.Tests.EditMode")]
[assembly: InternalsVisibleTo("Project.AI.Tests.PlayMode")]
