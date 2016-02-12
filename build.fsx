// include Fake lib
#r "packages/FAKE/tools/FakeLib.dll"
open System
open System.IO
open Fake
open Fake.Testing
open Fake.AssemblyInfoFile

// Properties
let buildDir = "./build/"

//http://semver.org
let version =
  sprintf "%s" "0.0.1"

// Targets
Target "Clean" (fun _ ->
    CleanDir buildDir
)

Target "Test" (fun _ ->
    !! (buildDir @@ "FSharp.SSEClient.Tests.dll")
      |> NUnit3 (fun p -> {p with ResultSpecs = [currentDirectory @@ "TestResult.xml;format=nunit2"]})
)

Target "Build" (fun _ ->

    CreateFSharpAssemblyInfo "./src/AssemblyInfo.fs"
        [Attribute.Title "FSharp.SSEClient"
         Attribute.Description "SSE Client"
         Attribute.Product "FSharp.SSEClient"
         Attribute.Version version
         Attribute.FileVersion version]

    !! "**/*.*sproj"
      |> MSBuildRelease buildDir "Build"
      |> Log "AppBuild-Output: "
)

// Dependencies
"Clean"
  ==> "Build"
  ==> "Test"
// start build
RunTargetOrDefault "Test"
