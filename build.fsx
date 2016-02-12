// include Fake lib
#r "packages/FAKE/tools/FakeLib.dll"
open System
open System.IO
open Fake
open Fake.Testing
open Fake.AssemblyInfoFile

// Properties
let buildDir = "./build/"
let testDir = "./test/"

//http://semver.org
let version =
  sprintf "%s" "0.0.1"

// Targets
Target "Clean" (fun _ ->
    CleanDir buildDir
    CleanDir testDir
)

Target "BuildTest" (fun _ ->
    !! "tests/FSharp.SSEClient.Tests.fsproj"
        |> MSBuildDebug testDir "Build"
        |> Log "App-Output: "
)

Target "Test" (fun _ ->
    !! (testDir @@ "FSharp.SSEClient.Tests.dll")
      |> NUnit3 (fun p -> {p with ResultSpecs = [currentDirectory @@ "TestResult.xml;format=nunit2"]})
)

Target "BuildSSEClient" (fun _ ->

    CreateFSharpAssemblyInfo "./src/AssemblyInfo.fs"
        [Attribute.Title "FSharp.SSEClient"
         Attribute.Description "SSE Client"
         Attribute.Product "FSharp.SSEClient"
         Attribute.Version version
         Attribute.FileVersion version]

    !! "src/FSharp.SSEClient.Tests.fsproj"
      |> MSBuildRelease buildDir "Build"
      |> Log "AppBuild-Output: "
)

// Dependencies
"Clean"
  ==> "BuildSSEClient"
  ==> "BuildTest" 
  ==> "Test"
// start build
RunTargetOrDefault "Test"
