using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General assembly metadata.
[assembly: AssemblyTitle("ModAPI")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("ModAPI")]
[assembly: AssemblyCopyright("Copyright 2019")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Types in this assembly are not visible to COM components.
[assembly: ComVisible(false)]

// Type library ID if this project is exposed to COM.
[assembly: Guid("3c99d6bf-a4a9-45dc-bef1-5717ece2a687")]

// Scenario RNG transpilers emit calls from the vanilla game assembly to internal
// domain-aware bridge overloads. Keep those implementation details out of ModAPI's
// public surface while allowing the rewritten call sites to pass CLR access checks.
[assembly: InternalsVisibleTo("Assembly-CSharp")]

// Assembly version format:
//
//      Major version
//      Minor version
//      Build number
//      Revision
//
// You can use "*" for the build and revision numbers.
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("2.0.0.0")]
[assembly: AssemblyFileVersion("2.0.0.0")]
[assembly: AssemblyInformationalVersion("2.0.0")]
