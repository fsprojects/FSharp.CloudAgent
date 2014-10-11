namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("FSharp.CloudAgent")>]
[<assembly: AssemblyProductAttribute("FSharp.CloudAgent")>]
[<assembly: AssemblyDescriptionAttribute("Allows the use of distributed F# Agents in Azure.")>]
[<assembly: AssemblyVersionAttribute("0.1")>]
[<assembly: AssemblyFileVersionAttribute("0.1")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.1"
