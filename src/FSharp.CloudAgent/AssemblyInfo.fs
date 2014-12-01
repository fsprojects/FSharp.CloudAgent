namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("FSharp.CloudAgent")>]
[<assembly: AssemblyProductAttribute("FSharp.CloudAgent")>]
[<assembly: AssemblyDescriptionAttribute("Allows the use of distributed F# Agents in Azure.")>]
[<assembly: AssemblyVersionAttribute("0.3")>]
[<assembly: AssemblyFileVersionAttribute("0.3")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.3"
