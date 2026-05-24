# `Auto-dot-github.yml` Update Notification

> [!TIP]
> Update this file will notify the listener repos of changes.

```yml
name: '[Auto] Repo Gardening'

on:
  schedule:
    - cron: "0 23 * * *"
  pull_request:  # PR labeler
    types:
      - opened
      - edited
      - closed
      - reopened
  release:
    types:
      - published  # Auto changelog
  # NOTE: draft release won't trigger any 'release' event.
  #       For workaround, use dispatch event to invoke auto-bump action
  workflow_dispatch:
    inputs:
      auto-bump-version:
        type: string
        required: false
        description: |
          Specify version for Auto Bump.
          Leave empty to invoke Sync and Auto Changelog.
      auto-bump-args:
        type: string
        required: false
        # DEBUG
        description: '**DEBUG**: NOT WORKING'
        default: csproj=GlobalPackageVersion
  workflow_call:   # Auto bump
    inputs:
      auto-bump-version:
        type: string
        required: false  # False for backward compatibility
      auto-bump-args:
        type: string
        required: false  # False for backward compatibility
        description: |

          Auto bump PR

          - npm (any non-empty string)
            Always executed (best-effort).
            --> npm version "VERSION_WITHOUT_v_PREFIX" --no-git-tag-version

          - csproj=XmlTagName (e.g. csproj=MyVersionProperty)
            Finds the current value from the first matching .csproj/.props file.
            --> Then performs plain text replacement.
            --> NOTE: This is a naive string replacement (not XML-aware, not schema-aware).
                Carefully review PR.

jobs:
  main:
    uses: sator-imaging/.github/.github/workflows/Auto-dot-github.yml@main
    with:

      # See above
      auto-bump-args: $$$  # <-- UPDATE HERE

      auto-bump-version: ${{ inputs.auto-bump-version }}
    secrets: inherit
    permissions:
      pull-requests: write
      contents: write  # Sync .github | Auto changelog | Auto bump
      issues: write    # PR labeler
```
