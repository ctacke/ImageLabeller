# Release Process

## Creating a New Release

This project uses GitHub Actions to automatically build and publish releases when you push a version tag.

### Steps to Create a Release:

1. **Update the version number** in `Source/ImageLabeller/ImageLabeller.csproj`:
   ```xml
   <Version>1.0.0</Version>
   <AssemblyVersion>1.0.0</AssemblyVersion>
   <FileVersion>1.0.0</FileVersion>
   ```

2. **Commit the version change**:
   ```bash
   git add Source/ImageLabeller/ImageLabeller.csproj
   git commit -m "Bump version to 1.0.0"
   ```

3. **Create and push a tag**:
   ```bash
   git tag v1.0.0
   git push origin v1.0.0
   ```

4. **Wait for the GitHub Action to complete**:
   - The action will automatically build for Windows, Linux, and macOS
   - It will create a GitHub release with downloadable binaries
   - Check the "Actions" tab in GitHub to monitor progress

### What Gets Built

The GitHub Action creates self-contained executables for:
- **Windows** (x64): `ImageLabeller-win-x64.zip`
- **Linux** (x64): `ImageLabeller-linux-x64.tar.gz`
- **macOS** (x64): `ImageLabeller-osx-x64.tar.gz`

Each package includes everything needed to run the app without installing .NET separately.

### Version Numbering

We follow semantic versioning (MAJOR.MINOR.PATCH):
- **MAJOR**: Breaking changes
- **MINOR**: New features (backward compatible)
- **PATCH**: Bug fixes

### Troubleshooting

If the build fails:
1. Check the Actions tab in GitHub for error logs
2. Test the build locally first: `dotnet publish Source/ImageLabeller/ImageLabeller.csproj -c Release`
3. Make sure all dependencies are properly referenced in the .csproj file
