# renovate-config

This repository contains the shared renovate configurations.

The shared configurations are baselines. Each project is free to set their own rules on top of this configuration.

## Usage

### GitHub projects

````json
{
  "$schema": "https://docs.renovatebot.com/renovate-schema.json",
  "extends": [
    "github>gsoft-inc/renovate-config"
  ]
}
````

#### Enabling Auto-Merge Functionality for GitHub

Auto-merge has been set up using the [branch approach](https://docs.renovatebot.com/key-concepts/automerge/#branch-vs-pr-automerging), chosen to minimize noise and allow for the bypassing of PR review requirements.

For those utilizing branch protection rules on the default branch, specific adjustments are necessary to facilitate auto-merge capabilities for GitHub:

1. **Update Branch Policies**: Modify your branch protection settings to permit the account or service running Renovate to bypass pull request review requirements.

2. **Handling Status Checks**: If your repository enforces "Require status checks to pass before merging", be aware that Renovate will be unable to merge changes into the target branch if any of these checks fail. It will fallback to create a pull request.

### Azure DevOps

````json
{
  "$schema": "https://docs.renovatebot.com/renovate-schema.json",
  "platform": "azure",
  "endpoint": "https://dev.azure.com/YOUR_ORGANIZATION",
  "repositories": [
    "YOUR_REPO"
  ],
  "extends": [
      "github>gsoft-inc/renovate-config"
  ]
}
````

You also need to provide a GitHub token in the CI:

````yaml
trigger: none
schedules:
  - cron: "13 22 * * *"
    displayName: "Daily run"
    branches:
      include:
        - main
    always: true

resources:
  repositories:
  - repository: self
    type: git
  - repository: templates
    type: git
    name: Shared-Assets/Pipeline-Library
    ref: refs/tags/1.2.3

pool: 'Azure Pipelines'

variables:
- group: renovate

steps:
- template: steps/renovate/renovate-template.yml@templates
  parameters:
    githubToken: $(GITHUB_COM_TOKEN)
````

## System tests
In order to run the system tests you will need the `workflow` scope on this repository.

To request this scope you can run the following command in your CLI
```
gh auth login --scopes workflow
```

If you do not have this scope, you will run into the following error
```
! [remote rejected] main -> main (refusing to allow an OAuth App to create or update workflow `.github/workflows/ci.yml` without `workflow` scope)
```