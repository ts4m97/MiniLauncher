# Releasing

MiniLauncher uses GitHub Releases for downloadable builds and GitHub Packages for a NuGet-distributed portable package.

## Create a Release

Tag a version and push it:

```powershell
git tag v0.1.0
git push origin v0.1.0
```

The `Release` workflow will:

- publish a self-contained `win-x64` build
- create `MiniLauncher-win-x64.zip`
- create a GitHub Release with the zip attached
- publish `MiniLauncher.Portable` to GitHub Packages

## Manual Release Workflow

The release workflow can also be started manually from the GitHub Actions tab. Manual runs build and upload the artifact, but only tag runs create a GitHub Release and publish a package.
