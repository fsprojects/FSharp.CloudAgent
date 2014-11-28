namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("FSharp.CloudAgent")>]
[<assembly: AssemblyProductAttribute("FSharp.CloudAgent")>]
[<assembly: AssemblyDescriptionAttribute("Allows the use of distributed F# Agents in Azure.")>]
[<assembly: AssemblyVersionAttribute("0.2")>]
[<assembly: AssemblyFileVersionAttribute("0.2")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.2"
