﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class AspNetCoreOnFullFramework : SdkTest
    {
        public AspNetCoreOnFullFramework(ITestOutputHelper log) : base(log)
        {
        }

        [WindowsOnlyTheory]
        [InlineData("1.1.2")]
        [InlineData("2.0.4")]
        public void It_discovers_assembly_parts(string aspnetVersion)
        {
            var testProject = new TestProject()
            {
                Name = "AssemblyPartDiscovery",
                IsSdkProject = true,
                TargetFrameworks = "net461",
                IsExe = true
            };

            testProject.SourceFiles["Program.cs"] = @"
using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.Extensions.DependencyModel;
using System.IO;
using System.Linq;

public class Program
{
    public static void Main(string[] args)
    {
        var parts = DefaultAssemblyPartDiscoveryProvider.DiscoverAssemblyParts(""" + testProject.Name + @""");
        foreach (var item in parts)
        {
            System.Console.WriteLine(item.Name);
        }
    }
}";

            TestProject referencedProjectWithPart = new TestProject()
            {
                Name = "ReferencedProjectWithPart",
                IsSdkProject = true,
                TargetFrameworks = "net461",
                IsExe = false
            };

            
            referencedProjectWithPart.References.Add("System.ServiceModel");

            referencedProjectWithPart.SourceFiles["Class1.cs"] = @"
class Class1
{
    public string X => typeof(System.ServiceModel.AddressFilterMode).ToString();
}";

            TestProject referencedProjectWithMvc = new TestProject()
            {
                Name = "ReferencedProjectWithMVC",
                IsSdkProject = true,
                ProjectSdk = "Microsoft.NET.Sdk.Web",
                TargetFrameworks = "net461",
                IsExe = false
            };

            referencedProjectWithMvc.PackageReferences.Add(new TestPackageReference("Microsoft.AspNetCore.Mvc", aspnetVersion));

            testProject.ReferencedProjects.Add(referencedProjectWithPart);
            testProject.ReferencedProjects.Add(referencedProjectWithMvc);

            var testProjectInstance = _testAssetsManager
                .CreateTestProject(testProject, identifier: aspnetVersion)
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, testProjectInstance.TestRoot, testProject.Name);

            buildCommand.Execute()
                .Should()
                .Pass();

            string outputPath = buildCommand.GetOutputDirectory(testProject.TargetFrameworks).FullName;

            string exePath = Path.Combine(outputPath, testProject.Name + ".exe");

            var toolCommandSpec = new SdkCommandSpec()
            {
                FileName = exePath
            };
            TestContext.Current.AddTestEnvironmentVariables(toolCommandSpec);

            ICommand toolCommand = toolCommandSpec.ToCommand().CaptureStdOut();

            var toolResult = toolCommand.Execute();

            toolResult.Should().Pass();
        }
    }
}
