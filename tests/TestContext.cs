using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using CliWrap;
using GitHub.Octokit.Client;
using GitHub.Octokit.Client.Authentication;
using Markdig;
using Markdig.Extensions.Tables;
using Meziantou.Framework;
using Meziantou.Framework.InlineSnapshotTesting;
using Octokit;
using Octokit.Internal;
using Xunit.Abstractions;
using GitHubClient = GitHub.GitHubClient;

namespace renovate_config.tests;

internal sealed class TestContext(
    ITestOutputHelper outputHelper,
    TemporaryDirectory temporaryDirectory,
    GitHubClient gitHubClient,
    Octokit.GitHubClient legacyGitHubClient) : IAsyncDisposable
{
    private static GitHubClient? _sharedGitHubClient;

    private const string DefaultBranchName = "main";
    private const string RepositoryOwner = "gsoft-inc";
    private const string RepositoryName = "renovate-config-test";
    private readonly string _repoPath = temporaryDirectory.FullPath;

    public static async Task<TestContext> CreateAsync(ITestOutputHelper outputHelper)
    {
        var gitHubClient = await GetGitHubClient(outputHelper);
        var legacyGitHubClient = await GetLegacyGitHubClient(outputHelper);

        await ExecuteCommand(outputHelper, "gh", ["auth", "status"]);

        var temporaryDirectory = TemporaryDirectory.Create();
        var repoPath = temporaryDirectory.FullPath;
        await ExecuteCommand(outputHelper, "git", ["-C", repoPath, "init", "--initial-branch=main"]);

        CopyRenovateFile(temporaryDirectory);

        return new TestContext(outputHelper, temporaryDirectory, gitHubClient, legacyGitHubClient);
    }

    public void AddFile(string path, string content)
    {
        temporaryDirectory.CreateTextFile(path, content);
    }

    public void AddInternalDeveloperPlatformCodeOwnersFile()
    {
        temporaryDirectory.CreateTextFile("CODEOWNERS",
            """
            * @gsoft-inc/internal-developer-platform
            """);
    }

    public void AddSuccessfulWorkflowFileToSatisfyBranchPolicy()
    {
        temporaryDirectory.CreateTextFile(".github/workflows/ci.yml",
            /*lang=yaml*/"""
            name: CI
            on:
                pull_request: 
                    branches:
                        - "*"
                push:
                    branches:
                        - "renovate/**"
                        
            jobs:
                build:
                    runs-on: ubuntu-latest
                    steps:
                        - name: Dummy successful step
                          run: sleep 1
            """
            );
    }

    public void AddFailingWorklowFileToSatisfyBranchPolicy()
    {
        temporaryDirectory.CreateTextFile(".github/workflows/ci.yml",
            /*lang=yaml*/"""
            name: CI
            on:
                pull_request: 
                    branches:
                        - "*"
                push:
                    branches:
                        - "renovate/**"
                        
            jobs:
                build:
                    runs-on: ubuntu-latest
                    steps:
                        - name: Dummy failing step
                          run: exit 1
            """
        );
    }

    public async Task PushFilesOnDefaultBranch()
    {
        var token = await GetGitHubToken(outputHelper);
        var gitUrl = $"https://{token}@github.com/gsoft-inc/renovate-config-test";

        await this.CleanupRepository();

        await ExecuteCommand(outputHelper, "git", ["-C", this._repoPath, "add", "."]);
        await ExecuteCommand(outputHelper, "git", ["-C", this._repoPath, "-c", "user.email=idp@workleap.com", "-c", "user.name=IDP ScaffoldIt", "commit", "--message", "IDP ScaffoldIt automated test"]);
        await ExecuteCommand(outputHelper, "git", ["-C", this._repoPath, "push", gitUrl, DefaultBranchName + ":" + DefaultBranchName, "--force"]);
    }

    public void UseRenovateFile(string filename)
    {
        var gitRoot = GetGitRoot();
        var filePath = temporaryDirectory.FullPath / "renovate.json";

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        File.Copy(gitRoot / filename, filePath);
    }

    public async Task RunRenovate()
    {
        var token = await GetGitHubToken(outputHelper);

        await ExecuteCommand(
            outputHelper,
            "docker",
            [
                "run",
                "--rm",
                "-e", "LOG_LEVEL=debug",
                "-e", "RENOVATE_PRINT_CONFIG=true",
                "-e", $"RENOVATE_TOKEN={token}",
                "-e", "RENOVATE_RECREATE_WHEN=always",
                "-e", "RENOVATE_PR_HOURLY_LIMIT=0",
                "-e", "RENOVATE_PR_CONCURRENT_LIMIT=0",
                "-e", "RENOVATE_BRANCH_CONCURRENT_LIMIT=0",
                "-e", "RENOVATE_LABELS=[\"renovate\"]",
                "-e", "RENOVATE_INHERIT_CONFIG_FILE_NAME=not-renovate.json",
                "-e", "RENOVATE_REPOSITORIES=[\"https://github.com/gsoft-inc/renovate-config-test\"]",
                "--pull", "always",
                "renovate/renovate:latest",
                "renovate",
                "gsoft-inc/renovate-config-test"
            ]);
    }

    [InlineSnapshotAssertion(nameof(expected))]
    [SuppressMessage("ReSharper", "ExplicitCallerInfoArgument", Justification = "We want to forward the caller info")]
    public async Task AssertPullRequests(string? expected = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = -1)
    {
        var pullRequests = await this.GetPullRequests();
        InlineSnapshot
            .WithSettings(settings => settings.ScrubLinesWithReplace(line => Regex.Replace(line, "to [^ ]+ ?", "to redacted")))
            .Validate(pullRequests, expected, filePath, lineNumber);
    }

    [InlineSnapshotAssertion(nameof(expected))]
    [SuppressMessage("ReSharper", "ExplicitCallerInfoArgument", Justification = "We want to forward the caller info")]
    public async Task AssertCommits(string? expected = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = -1)
    {
        var commits = await this.GetCommits();
        InlineSnapshot
            .WithSettings(settings => settings.ScrubLinesWithReplace(line => Regex.Replace(line, "to [^ ]+ ?", "to redacted")))
            .Validate(commits, expected, filePath, lineNumber);
    }

    private async Task<IEnumerable<PullRequestInfos>> GetPullRequests()
    {
        var pullRequests = await gitHubClient.Repos[RepositoryOwner][RepositoryName].Pulls.GetAsync() ?? [];

        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

        var pullRequestsInfos = new List<PullRequestInfos>(pullRequests.Count);

        foreach (var pullRequest in pullRequests.OrderBy(x => x.Title))
        {
            var markdownDocument = Markdown.Parse(pullRequest.Body!, pipeline);
            var prTitle = pullRequest.Title;
            var prLabels = pullRequest.Labels?.Select(x => x.Name).Order();

            var table = markdownDocument.OfType<Table>().First();
            var rows = table.Skip(1).OfType<TableRow>().ToArray();

            var packageUpdateInfos = new List<PackageUpdateInfos>(rows.Length);

            foreach (var row in rows)
            {
                var package = row[0].InnerText().Replace("( source )", string.Empty).Trim();
                var type = row[1].InnerText().Trim();
                var update = row[2].InnerText().Trim();

                packageUpdateInfos.Add(new PackageUpdateInfos(package, type, update));
            }

            pullRequestsInfos.Add(new PullRequestInfos(
                prTitle!,
                prLabels!,
                packageUpdateInfos.OrderBy(x => x.Package).ThenBy(x => x.Type).ThenBy(x => x.Update),
                pullRequest.AutoMerge != null
                ));
        }

        return pullRequestsInfos;
    }

    private async Task<IEnumerable<CommitInfo>> GetCommits()
    {
        var commits = await gitHubClient.Repos[RepositoryOwner][RepositoryName].Commits.GetAsync() ?? [];

        var commitInfos = new List<CommitInfo>(commits.Count);

        foreach (var commit in commits)
        {
            var message = commit.CommitProp!.Message ?? string.Empty;

            commitInfos.Add(new CommitInfo(message));
        }

        return commitInfos;
    }

    public async Task WaitForBranchPolicyChecksToSucceed()
    {
        var branches = await gitHubClient.Repos[RepositoryOwner][RepositoryName].Branches.GetAsync() ?? [];

        foreach (var branch in branches)
        {
            if (branch.Name != DefaultBranchName)
            {
                await this.WaitForCommitAssociatedWorkflowsToSucceed(branch.Commit!.Sha!);
            }
        }
    }

    // Can't uses commit checks directly since fined grained permission token does not support checks scopes
    // Related issue: https://github.com/cli/cli/issues/8842
    private async Task WaitForCommitAssociatedWorkflowsToSucceed(string commitSha)
    {
        bool isCommitStatusCompleted;

        do
        {
            var workflows = await legacyGitHubClient.Actions.Workflows.Runs.List(RepositoryOwner, RepositoryName,
                new WorkflowRunsRequest { HeadSha = commitSha });

            isCommitStatusCompleted =
                workflows.WorkflowRuns.Any() &&
                workflows.WorkflowRuns!.All(x => x.Status == WorkflowRunStatus.Completed);

            if (!isCommitStatusCompleted)
            {
                await Task.Delay(1000);
            }
        } while (!isCommitStatusCompleted);
    }

    private async Task CleanupRepository()
    {
        var branches = await gitHubClient.Repos[RepositoryOwner][RepositoryName].Branches.GetAsync() ?? [];
        foreach (var branch in branches)
        {
            outputHelper.WriteLine($"Deleting branch: {branch.Name}");

            try
            {
                await gitHubClient.Repos[RepositoryOwner][RepositoryName].Git.Refs[$"heads/{branch.Name}"].DeleteAsync();
            }
            catch (Exception)
            {
                // Ignore if it doesn't exist
                outputHelper.WriteLine($"Deleting branch was not found: {branch.Name}");
            }
        }
    }

    private static async Task<string> GetGitHubToken(ITestOutputHelper outputHelper)
    {
        var token = Environment.GetEnvironmentVariable("GH_TOKEN");
        if (string.IsNullOrEmpty(token))
        {
            var (stdout, _) = await ExecuteCommand(outputHelper, "gh", ["auth", "token"]);

            token = stdout.Trim();
        }

        if (string.IsNullOrEmpty(token))
        {
            throw new Exception("GitHub token not found, run `gh auth login` to authenticate");
        }

        return token;
    }

    private static async Task<Octokit.GitHubClient> GetLegacyGitHubClient(ITestOutputHelper outputHelper)
    {
        var token = await GetGitHubToken(outputHelper);

        var githubClient = new Octokit.GitHubClient(new ProductHeaderValue("renovate-test"), new InMemoryCredentialStore(new Octokit.Credentials(token)));

        return githubClient;
    }

    private static async Task<GitHubClient> GetGitHubClient(ITestOutputHelper outputHelper)
    {
        if (_sharedGitHubClient != null)
        {
            return _sharedGitHubClient;
        }

        var token = await GetGitHubToken(outputHelper);
        var tokenProvider = new TokenProvider(token);
        var adapter = RequestAdapter.Create(new TokenAuthProvider(tokenProvider));
        _sharedGitHubClient = new GitHubClient(adapter);

        return _sharedGitHubClient;
    }

    private static void CopyRenovateFile(TemporaryDirectory temporaryDirectory)
    {
        var gitRoot = GetGitRoot();

        File.Copy(gitRoot / "default.json", temporaryDirectory.FullPath / "renovate.json");
    }

    private static FullPath GetGitRoot()
    {
        if (FullPath.CurrentDirectory().TryFindFirstAncestorOrSelf(current => Directory.Exists(current / ".git"), out var root))
        {
            return root;
        }

        throw new InvalidOperationException("root folder not found");
    }

    private static async Task<(string StdOut, string StdError)> ExecuteCommand(ITestOutputHelper outputHelper,
        string executable, string[] args, KeyValuePair<string, string>[]? envVariables = null)
    {
        var stdOut = new StringBuilder();
        var stdErr = new StringBuilder();
        await Cli.Wrap(executable).WithArguments(args)
            .WithEnvironmentVariables(builder =>
            {
                foreach (var (key, value) in envVariables ?? [])
                {
                    builder.Set(key, value);
                }
            })
            .WithStandardOutputPipe(PipeTarget.Merge(PipeTarget.ToStringBuilder(stdOut), PipeTarget.ToDelegate(outputHelper.WriteLine)))
            .WithStandardErrorPipe(PipeTarget.Merge(PipeTarget.ToStringBuilder(stdErr), PipeTarget.ToDelegate(outputHelper.WriteLine)))
            .ExecuteAsync();

        return (stdOut.ToString(), stdErr.ToString());
    }

    public async ValueTask DisposeAsync()
    {
        await temporaryDirectory.DisposeAsync();
        await this.CleanupRepository();
    }
}