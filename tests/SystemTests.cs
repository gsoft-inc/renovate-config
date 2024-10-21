using Xunit.Abstractions;

namespace renovate_config.tests;

public sealed class SystemTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task Given_Various_Updates_When_No_AutoMerges_Then_Opens_PRs()
    {
        await using var testContext = await TestContext.CreateAsync(testOutputHelper);

        testContext.AddSuccessfulWorkflowFileToSatisfyBranchPolicy();
        testContext.AddFile("global.json", /*lang=json*/"""{"sdk": {"version": "6.0.100"}}""");

        testContext.AddFile("Dockerfile",
            """
            FROM mcr.microsoft.com/dotnet/sdk:6.0.100
            FROM mcr.microsoft.com/dotnet/aspnet:6.0.0
            """);

        testContext.AddFile("project.csproj",
            /*lang=xml*/"""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="System.Text.Json" Version="7.0.0" />
                <PackageReference Include="Workleap.Extensions.Mongo" Version="1.11.0" />
              </ItemGroup>
            </Project>
            """);

        testContext.AddFile("package.json",
            /*lang=json*/"""
             {
               "dependencies": {
                 "@squide/core": "5.2.0"
               }
             }
             """);

        await testContext.PushFilesOnDefaultBranch();
        await testContext.RunRenovate();

        // Need to run renovate a second time so that branch is merged
        await testContext.WaitForBranchPolicyChecksToSucceed();
        await testContext.RunRenovate();

        await testContext.AssertPullRequests(
            """
            - Title: chore(deps): update dependency system.text.json to redacted[security]
              Labels:
                - security
              PackageUpdatesInfos:
                - Package: System.Text.Json
                  Type: nuget
                  Update: major
            - Title: chore(deps): update dependency workleap.extensions.mongo to redacted
              Labels:
                - renovate
              PackageUpdatesInfos:
                - Package: Workleap.Extensions.Mongo
                  Type: nuget
                  Update: patch
            - Title: chore(deps): update dotnet-sdk
              Labels:
                - renovate
              PackageUpdatesInfos:
                - Package: dotnet-sdk
                  Type: dotnet-sdk
                  Update: patch
                - Package: mcr.microsoft.com/dotnet/aspnet
                  Type: final
                  Update: patch
                - Package: mcr.microsoft.com/dotnet/sdk
                  Type: stage
                  Update: patch
            - Title: chore(deps): update dotnet-sdk to redacted(major)
              Labels:
                - renovate
              PackageUpdatesInfos:
                - Package: dotnet-sdk
                  Type: dotnet-sdk
                  Update: major
                - Package: mcr.microsoft.com/dotnet/aspnet
                  Type: final
                  Update: major
                - Package: mcr.microsoft.com/dotnet/sdk
                  Type: stage
                  Update: major
            - Title: fix(deps): update dependency @squide/core to redacted
              Labels:
                - renovate
              PackageUpdatesInfos:
                - Package: @squide/core
                  Type: dependencies
                  Update: minor
            """);
    }

    [Fact]
    public async Task Given_Various_Updates_When_AutoMerge_Enabled_Then_Only_Major_PR_Opened()
    {
        await using var testContext = await TestContext.CreateAsync(testOutputHelper);
        testContext.UseRenovateFile("all-automerge.json");

        testContext.AddSuccessfulWorkflowFileToSatisfyBranchPolicy();

        testContext.AddFile("global.json", /*lang=json*/"""{"sdk": {"version": "6.0.100"}}""");

        testContext.AddFile("Dockerfile",
            """
            FROM mcr.microsoft.com/dotnet/sdk:6.0.100
            FROM mcr.microsoft.com/dotnet/aspnet:6.0.0
            """);

        testContext.AddFile("project.csproj",
            /*lang=xml*/"""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="System.Text.Json" Version="7.0.0" />
                <PackageReference Include="Workleap.Extensions.Mongo" Version="1.11.0" />
              </ItemGroup>
            </Project>
            """);

        testContext.AddFile("package.json",
            /*lang=json*/"""
             {
               "dependencies": {
                 "@squide/core": "5.2.0"
               }
             }
             """);

        await testContext.PushFilesOnDefaultBranch();
        await testContext.RunRenovate();

        // Need to run renovate a second time so that branch is merged
        // Need to pull commit status to see is check is completed
        await testContext.WaitForBranchPolicyChecksToSucceed();
        await testContext.RunRenovate();

        await testContext.WaitForBranchPolicyChecksToSucceed();
        await testContext.RunRenovate();

        await testContext.AssertPullRequests(
            """
            - Title: Update dependency dotnet-sdk to redacted
              Labels:
                - renovate
              PackageUpdatesInfos:
                - Package: dotnet-sdk
                  Type: dotnet-sdk
                  Update: major
            - Title: Update dependency System.Text.Json to redacted[SECURITY]
              Labels:
                - renovate
              PackageUpdatesInfos:
                - Package: System.Text.Json
                  Type: nuget
                  Update: major
            - Title: Update mcr.microsoft.com/dotnet/aspnet Docker tag to redacted
              Labels:
                - renovate
              PackageUpdatesInfos:
                - Package: mcr.microsoft.com/dotnet/aspnet
                  Type: final
                  Update: major
            - Title: Update mcr.microsoft.com/dotnet/sdk Docker tag to redacted
              Labels:
                - renovate
              PackageUpdatesInfos:
                - Package: mcr.microsoft.com/dotnet/sdk
                  Type: stage
                  Update: major
            """);

        await testContext.AssertCommits(
            """
            - Message: Update dependency dotnet-sdk to redacted
            - Message: Update dependency Workleap.Extensions.Mongo to redacted
            - Message: IDP ScaffoldIt automated test
            """);
    }

    [Fact]
    public async Task Given_Hangfire_Updates_Then_Groups_Them_In_Single_PR()
    {
        await using var testContext = await TestContext.CreateAsync(testOutputHelper);

        testContext.AddFile("project.csproj",
            /*lang=xml*/"""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Hangfire" Version="1.7.1" />
                <PackageReference Include="Hangfire.AspNetCore" Version="1.7.1" />
                <PackageReference Include="Hangfire.Core" Version="1.7.1" />
                <PackageReference Include="Hangfire.NetCore" Version="1.7.1" />
                <PackageReference Include="Hangfire.SqlServer" Version="1.7.1" />
              </ItemGroup>
            </Project>
            """);

        await testContext.PushFilesOnDefaultBranch();
        await testContext.RunRenovate();

        await testContext.AssertPullRequests(
            """
            - Title: chore(deps): update hangfire monorepo to redacted
              Labels:
                - renovate
              PackageUpdatesInfos:
                - Package: Hangfire
                  Type: nuget
                  Update: minor
                - Package: Hangfire.AspNetCore
                  Type: nuget
                  Update: minor
                - Package: Hangfire.Core
                  Type: nuget
                  Update: minor
                - Package: Hangfire.NetCore
                  Type: nuget
                  Update: minor
                - Package: Hangfire.SqlServer
                  Type: nuget
                  Update: minor
            """);
    }

    [Fact]
    public async Task Given_Dotnet_ThirdParty_Dependencies_Updates_Then_AutoMerges_Minor_Updates()
    {
        await using var testContext = await TestContext.CreateAsync(testOutputHelper);
        testContext.UseRenovateFile("dotnet-trusted-thirdparty-dependencies-automerge.json");

        testContext.AddSuccessfulWorkflowFileToSatisfyBranchPolicy();

        testContext.AddFile("project.csproj",
            /*lang=xml*/"""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="MongoDB.Bson" Version="2.27.0" />
                <PackageReference Include="MongoDB.Driver" Version="2.27.0" />
                <PackageReference Include="MongoDB.Driver.Core" Version="2.27.0" />
                <PackageReference Include="coverlet.collector" Version="3.0.0">
                    <PrivateAssets>all</PrivateAssets>
                    <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
                </PackageReference>
                <PackageReference Include="xunit" Version="1.9.1" />
              </ItemGroup>
            </Project>
            """);

        await testContext.PushFilesOnDefaultBranch();
        await testContext.RunRenovate();

        // Need to run renovate a second time so that branch is merged
        await testContext.WaitForBranchPolicyChecksToSucceed();
        await testContext.RunRenovate();

        await testContext.AssertPullRequests(
            """
            - Title: Update microsoft (major)
              Labels:
                - renovate
              PackageUpdatesInfos:
                - Package: coverlet.collector
                  Type: nuget
                  Update: major
                - Package: MongoDB.Bson
                  Type: nuget
                  Update: major
                - Package: MongoDB.Driver
                  Type: nuget
                  Update: major
                - Package: xunit
                  Type: nuget
                  Update: major
            """);

        await testContext.AssertCommits(
            """
            - Message: Update microsoft
            - Message: IDP ScaffoldIt automated test
            """);
    }

    [Fact]
    public async Task Given_Ranged_Npm_Dependencies_Then_Pins_Them_In_Single_PR()
    {
        await using var testContext = await TestContext.CreateAsync(testOutputHelper);

        testContext.AddFile("package.json",
            /*lang=json*/"""
            {
              "dependencies": {
                "@azure/msal-browser": "^3.13.0",
                "@azure/msal-react": "~2.0.15"
              },
              "devDependencies": {
                "@storybook/addon-essentials": "^8.0.10",
                "@storybook/addon-interactions": "~8.0.10"
              }
            }
            """);

        await testContext.PushFilesOnDefaultBranch();
        await testContext.RunRenovate();

        await testContext.AssertPullRequests(
            """
            - Title: fix(deps): pin dependencies
              Labels:
                - renovate
              PackageUpdatesInfos:
                - Package: @azure/msal-browser
                  Type: dependencies
                  Update: pin
                - Package: @azure/msal-react
                  Type: dependencies
                  Update: pin
                - Package: @storybook/addon-essentials
                  Type: devDependencies
                  Update: pin
                - Package: @storybook/addon-interactions
                  Type: devDependencies
                  Update: pin
            """);
    }

    [Fact]
    public async Task Given_Microsoft_Dependencies_Updates_Then_Opens_Major_PR_And_AutoMerges_Minor()
    {
        await using var testContext = await TestContext.CreateAsync(testOutputHelper);
        testContext.UseRenovateFile("microsoft-automerge.json");

        testContext.AddSuccessfulWorkflowFileToSatisfyBranchPolicy();

        testContext.AddFile("project.csproj",
            /*lang=xml*/"""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="System.Text.Json" Version="7.0.0" />
                <PackageReference Include="Microsoft.ApplicationInsights.AspNetCore" Version="1.0.2" />
                <PackageReference Include="microsoft.AspNetCore.Authentication.OpenIdConnect" Version="7.0.0" />
                <PackageReference Include="Microsoft.Azure.AppConfiguration.AspNetCore" Version="7.0.0" />
                <PackageReference Include="Microsoft.CodeAnalysis.PublicApiAnalyzers" Version="3.3.4">
                  <PrivateAssets>all</PrivateAssets>
                  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
                </PackageReference>
              </ItemGroup>
            </Project>
            """);

        await testContext.PushFilesOnDefaultBranch();
        await testContext.RunRenovate();

        // Need to run renovate a second time so that branch is merged
        // Need to pull commit status to see is check is completed
        await testContext.WaitForBranchPolicyChecksToSucceed();
        await testContext.RunRenovate();

        await testContext.AssertPullRequests(
            """
            - Title: Update dependency System.Text.Json to redacted[SECURITY]
              Labels:
                - renovate
              PackageUpdatesInfos:
                - Package: System.Text.Json
                  Type: nuget
                  Update: major
            - Title: Update microsoft (major)
              Labels:
                - renovate
              PackageUpdatesInfos:
                - Package: Microsoft.ApplicationInsights.AspNetCore
                  Type: nuget
                  Update: major
                - Package: microsoft.AspNetCore.Authentication.OpenIdConnect
                  Type: nuget
                  Update: major
                - Package: Microsoft.Azure.AppConfiguration.AspNetCore
                  Type: nuget
                  Update: major
            """);

        await testContext.AssertCommits(
            """
            - Message: Update microsoft
            - Message: IDP ScaffoldIt automated test
            """);
    }

    [Fact]
    public async Task Given_Workleap_Dependencies_Updates_Then_Opens_Major_PR_And_AutoMerges_Minor()
    {
        await using var testContext = await TestContext.CreateAsync(testOutputHelper);
        testContext.UseRenovateFile("workleap-automerge.json");

        testContext.AddSuccessfulWorkflowFileToSatisfyBranchPolicy();

        testContext.AddFile("project.csproj",
            /*lang=xml*/"""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Workleap.Extensions.Mongo" Version="1.11.0" />
                <PackageReference Include="Workleap.Extensions.Http.Authentication.ClientCredentialsGrant" Version="1.3.0" />
                <PackageReference Include="Workleap.DomainEventPropagation.Abstractions" Version="0.2.0" />
              </ItemGroup>
            </Project>
            """);

        await testContext.PushFilesOnDefaultBranch();
        await testContext.RunRenovate();

        // Need to run renovate a second time so that branch is merged
        // Need to pull commit status to see is check is completed
        await testContext.WaitForBranchPolicyChecksToSucceed();
        await testContext.RunRenovate();

        await testContext.AssertPullRequests(
            """
            - Title: Update workleap (major)
              Labels:
                - renovate
              PackageUpdatesInfos:
                - Package: Workleap.DomainEventPropagation.Abstractions
                  Type: nuget
                  Update: major
                - Package: Workleap.Extensions.Http.Authentication.ClientCredentialsGrant
                  Type: nuget
                  Update: major
            """);

        await testContext.AssertCommits(
            """
            - Message: Update workleap
            - Message: IDP ScaffoldIt automated test
            """);
    }

    [Fact]
    public async Task Given_Microsoft_Minor_Dependencies_Update_When_CI_Succeed_Then_AutoMerge_By_Pushing_On_Main()
    {
        await using var testContext = await TestContext.CreateAsync(testOutputHelper);
        testContext.UseRenovateFile("microsoft-automerge.json");

        testContext.AddSuccessfulWorkflowFileToSatisfyBranchPolicy();

        testContext.AddInternalDeveloperPlatformCodeOwnersFile();

        testContext.AddFile("project.csproj",
            /*lang=xml*/"""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="System.Text.Json" Version="8.0.0" />
              </ItemGroup>
            </Project>
            """);

        await testContext.PushFilesOnDefaultBranch();
        await testContext.RunRenovate();

        // Need to run renovate a second time so that branch is merged
        // Need to pull commit status to see is check is completed
        await testContext.WaitForBranchPolicyChecksToSucceed();
        await testContext.RunRenovate();

        await testContext.AssertCommits(
            """
            - Message: Update dependency System.Text.Json to redacted[SECURITY]
            - Message: IDP ScaffoldIt automated test
            """);
    }

    [Fact]
    public async Task Given_Microsoft_Minor_Dependencies_Update_When_CI_Fail_Then_Abort_AutoMerge_And_Fallback_To_Create_PR()
    {
        await using var testContext = await TestContext.CreateAsync(testOutputHelper);
        testContext.UseRenovateFile("microsoft-automerge.json");

        testContext.AddFailingWorklowFileToSatisfyBranchPolicy();

        testContext.AddInternalDeveloperPlatformCodeOwnersFile();

        testContext.AddFile("project.csproj",
            /*lang=xml*/"""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="System.Text.Json" Version="8.0.0" />
              </ItemGroup>
            </Project>
            """);

        await testContext.PushFilesOnDefaultBranch();

        await testContext.RunRenovate();
        await testContext.WaitForBranchPolicyChecksToSucceed();

        // Need to run renovate a second time to create PR on CI failures
        await testContext.RunRenovate();

        await testContext.AssertPullRequests(
            """
            - Title: Update dependency System.Text.Json to redacted[SECURITY]
              Labels:
                - renovate
              PackageUpdatesInfos:
                - Package: System.Text.Json
                  Type: nuget
                  Update: patch
              IsAutoMergeEnabled: true
            """);
    }

    [Fact]
    public async Task Given_Workleap_Minor_Dependencies_Update_When_CI_Fail_Then_Abort_AutoMerge_And_Fallback_To_Create_PR()
    {
        await using var testContext = await TestContext.CreateAsync(testOutputHelper);
        testContext.UseRenovateFile("workleap-automerge.json");

        testContext.AddFailingWorklowFileToSatisfyBranchPolicy();

        testContext.AddInternalDeveloperPlatformCodeOwnersFile();

        testContext.AddFile("project.csproj",
            /*lang=xml*/"""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                  <PackageReference Include="Workleap.Extensions.Mongo" Version="1.11.0" />
              </ItemGroup>
            </Project>
            """);

        await testContext.PushFilesOnDefaultBranch();

        await testContext.RunRenovate();
        await testContext.WaitForBranchPolicyChecksToSucceed();

        // Need to run renovate a second time to create PR on CI failures
        await testContext.RunRenovate();

        await testContext.AssertPullRequests(
            """
            - Title: Update dependency Workleap.Extensions.Mongo to redacted
              Labels:
                - renovate
              PackageUpdatesInfos:
                - Package: Workleap.Extensions.Mongo
                  Type: nuget
                  Update: patch
              IsAutoMergeEnabled: true
            """);
    }

    [Fact]
    public async Task Given_Various_Dependencies_Updates_When_CI_Fail_Then_Abort_AutoMerge_And_Fallback_To_Create_PR()
    {
        await using var testContext = await TestContext.CreateAsync(testOutputHelper);
        testContext.UseRenovateFile("all-automerge.json");

        testContext.AddFailingWorklowFileToSatisfyBranchPolicy();

        testContext.AddFile("project.csproj",
            /*lang=xml*/"""
                        <Project Sdk="Microsoft.NET.Sdk">
                          <ItemGroup>
                            <PackageReference Include="Hangfire.NetCore" Version="1.7.1" />
                            <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
                            <PackageReference Include="Workleap.Extensions.Configuration.Substitution" Version="1.1.2" />
                          </ItemGroup>
                        </Project>
                        """);

        testContext.AddFile("package.json",
            /*lang=json*/"""
             {
               "dependencies": {
                 "@squide/core": "5.2.0"
               }
             }
             """);

        await testContext.PushFilesOnDefaultBranch();
        await testContext.RunRenovate();

        // Need to run renovate a second time so that branch is merged
        // Need to pull commit status to see is check is completed
        await testContext.WaitForBranchPolicyChecksToSucceed();
        await testContext.RunRenovate();

        await testContext.WaitForBranchPolicyChecksToSucceed();
        await testContext.RunRenovate();

        await testContext.AssertPullRequests(
            """
            - Title: Update dependency @squide/core to redacted
              Labels:
                - renovate
              PackageUpdatesInfos:
                - Package: @squide/core
                  Type: dependencies
                  Update: minor
              IsAutoMergeEnabled: true
            - Title: Update dependency Hangfire.NetCore to redacted
              Labels:
                - renovate
              PackageUpdatesInfos:
                - Package: Hangfire.NetCore
                  Type: nuget
                  Update: minor
              IsAutoMergeEnabled: true
            - Title: Update dependency Microsoft.Extensions.Logging.Abstractions to redacted
              Labels:
                - renovate
              PackageUpdatesInfos:
                - Package: Microsoft.Extensions.Logging.Abstractions
                  Type: nuget
                  Update: patch
              IsAutoMergeEnabled: true
            - Title: Update dependency Workleap.Extensions.Configuration.Substitution to redacted
              Labels:
                - renovate
              PackageUpdatesInfos:
                - Package: Workleap.Extensions.Configuration.Substitution
                  Type: nuget
                  Update: patch
              IsAutoMergeEnabled: true
            """);
    }

    [Fact]
    public async Task Given_GitVersion_Major_Update_Only_Then_Nothing_Happens_As_GitVersion_Major_Update_Is_Disabled()
    {
        await using var testContext = await TestContext.CreateAsync(testOutputHelper);

        testContext.AddFile("project.csproj",
            /*lang=xml*/"""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="GitVersion.MsBuild" Version="5.12.0" />
              </ItemGroup>
            </Project>
            """);

        await testContext.PushFilesOnDefaultBranch();
        await testContext.RunRenovate();

        await testContext.AssertPullRequests("[]");
    }
}