using System.Runtime.CompilerServices;

// The simulation context constructor and lifecycle helpers are internal so only the host drives them; the RPG core
// tests construct a context directly to exercise systems in isolation without the threaded host.
[assembly: InternalsVisibleTo("RPG.Core.Tests")]
