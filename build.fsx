#I "./packages/FAKE.1.62.1/tools"
#r "FakeLib.dll"

open Fake

// properties
let version = "0.4"
let projectName = "Firefly"
let projectDescription = "Firefly is a .NET HTTP Server in an assembly."
let authors = ["Louis DeJardin"]
  
let sourceDir = @".\src\"
let sourceAppDir = sourceDir + @"main\"
let sourceTestDir = sourceDir + @"test\"
let sourceSampleDir = sourceDir + @"sample\"

let targetDir = @".\target\"
let docsDir  = targetDir + @"docs\"
let buildDir  = targetDir + @"build\"
let testDir   = targetDir + @"test\"
let deployDir = targetDir + @"deploy\"
let nugetDir = targetDir + @"nuget\"

// files
let appReferences  = !! (sourceAppDir + @"**\*.csproj")
let testReferences = !! (sourceTestDir + @"**\*.csproj")

// tools
let nugetPath = @".\.nuget\nuget.exe"
let xunitPath = @".\tools\xunit-1.9\xunit.console.clr4.exe"
//let fxCopRoot = @".\Tools\FxCop\FxCopCmd.exe"


// build goals

Target "CleanTargetDir" (fun _ -> 
    CleanDirs [targetDir]
    CreateDir docsDir
    CreateDir buildDir
    CreateDir testDir
    CreateDir deployDir
    CreateDir nugetDir
)

Target "ApplyVersion" (fun _ ->
    let apply files =
        for file in files do
            ReplaceAssemblyInfoVersions (fun p ->
                {p with 
                    AssemblyVersion = version;
                    AssemblyFileVersion = version;
                    OutputFileName = file; })

    !! "./src/**/AssemblyInfo.cs" |> apply
)

Target "CompileApp" (fun _ ->
    MSBuild buildDir "Build" ["Configuration","Release"; "PackageVersion",version] appReferences
        |> Log "AppBuild-Output: "
)

Target "CompileTest" (fun _ ->
    MSBuild testDir "Build"  ["Configuration","Debug"] testReferences
        |> Log "TestBuild-Output: "
)

//Target "NUnitTest" (fun _ ->  
//    !! (testDir + @"\NUnit.Test.*.dll")
//        |> NUnit (fun p -> 
//            {p with 
//                ToolPath = nunitPath; 
//                DisableShadowCopy = true; 
//                OutputFile = testDir + @"TestResults.xml"})
//)

Target "xUnitTest" (fun _ ->  
    !! (testDir + @"\*.Tests.dll")
        |> xUnit (fun p -> 
            {p with 
                ToolPath = xunitPath;
                ShadowCopy = false;
                HtmlOutput = true;
                XmlOutput = true;
                OutputDir = testDir })
)

//Target "FxCop" (fun _ ->
//    !+ (buildDir + @"\**\*.dll") 
//        ++ (buildDir + @"\**\*.exe") 
//        |> Scan  
//        |> FxCop (fun p -> 
//            {p with                     
//                ReportFileName = testDir + "FXCopResults.xml";
//                ToolPath = fxCopRoot})
//)

Target "PackageZip" (fun _ ->
    CreateDir deployDir

    !+ (buildDir + "\**\*.*") 
        -- "*.zip" 
        |> Scan
        |> Zip buildDir (deployDir + "Firefly." + version + ".zip")
)

Target "PackageNuGet" (fun _ ->
    XCopy docsDir (nugetDir @@ "docs/")
    XCopy buildDir (nugetDir @@ "build/")

    NuGet (fun p -> 
        {p with 
            ToolPath = nugetPath              
            Version = version
            Project = projectName
            Description = projectDescription                               
            Authors = authors
            Dependencies = []
            OutputPath = nugetDir
            AccessKey = getBuildParamOrDefault "nugetkey" ""
            Publish = hasBuildParam "nugetkey" })  "Firefly.nuspec"
)


Target "InstallPackages" (fun _ ->
  let target = (environVar "HOME") @@ ".nuget";
  let apply files =
    for file in files do
      CopyFile target file

  !! (nugetDir @@ "*.nupkg") |> apply
)


Target "UploadPackages" (fun _ ->
  let apply files =
    for file in files do
      ExecProcess (fun info ->
                info.FileName <- nugetPath
                info.WorkingDirectory <- nugetDir |> FullName
                info.Arguments <-  sprintf "push \"%s\"" (file |> FullName)) (System.TimeSpan.FromMinutes 5.)

  !! (nugetDir @@ "*.nupkg") |> apply
)



let Phase name = (
  Target name (fun _ -> trace "----------")
  name
)

// build phases
Phase "Clean"
  ==> Phase "Initialize" 
  ==> Phase "Process" 
  ==> Phase "Compile" 
  ==> Phase "Test" 
  ==> Phase "Package"
  ==> Phase "Install"
  ==> Phase "Deploy" 

Phase "Default" <== ["Package"]

// build phase goals
"Clean" <== ["CleanTargetDir"]
"Process" <== ["ApplyVersion"]
"Compile" <== ["CompileApp"; "CompileTest"]
"Test" <== ["xUnitTest"]
"Package" <== ["PackageZip"; "PackageNuGet"]
"Install" <== ["InstallPackages"]
"Deploy" <== ["UploadPackages"]

// start build
Run <| getBuildParamOrDefault "target" "Default"

