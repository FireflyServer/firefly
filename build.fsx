#I "./packages/FAKE.1.62.1/tools"
#r "FakeLib.dll"

open Fake

// properties
let version = "0.2"
let projectName = "Dragonfly"
let projectDescription = "Dragonfly is a .NET HTTP Server in an assembly."
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
//    AssemblyInfo 
//        (fun p -> 
//        {p with
//            CodeLanguage = CSharp;
//            AssemblyVersion = version;
//            AssemblyTitle = "Calculator Command line tool";
//            AssemblyDescription = "Sample project for FAKE - F# MAKE";
//            Guid = "A539B42C-CB9F-4a23-8E57-AF4E7CEE5BAA";
//            OutputFileName = @".\src\app\Calculator\Properties\AssemblyInfo.cs"})
//
//    AssemblyInfo 
//        (fun p -> 
//        {p with
//            CodeLanguage = CSharp;
//            AssemblyVersion = version;
//            AssemblyTitle = "Calculator library";
//            AssemblyDescription = "Sample project for FAKE - F# MAKE";
//            Guid = "EE5621DB-B86B-44eb-987F-9C94BCC98441";
//            OutputFileName = @".\src\app\CalculatorLib\Properties\AssemblyInfo.cs"})          

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
        |> Zip buildDir (deployDir + "Dragonfly." + version + ".zip")
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
            Dependencies = ["Gate.Owin", "0.2.1"]
            OutputPath = nugetDir
            AccessKey = getBuildParamOrDefault "nugetkey" ""
            Publish = hasBuildParam "nugetkey" })  "Dragonfly.nuspec"
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

// start build
Run <| getBuildParamOrDefault "target" "Default"

