namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("FSharp.CloudAgent")>]
[<assembly: AssemblyProductAttribute("FSharp.CloudAgent")>]
[<assembly: AssemblyDescriptionAttribute("Allows the use of F# Agents in Azure")>]
[<assembly: AssemblyVersionAttribute("1.0")>]
[<assembly: AssemblyFileVersionAttribute("1.0")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "1.0"
