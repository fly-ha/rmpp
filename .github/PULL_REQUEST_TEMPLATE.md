## OpenSpec

- Change id: `build-offline-wysiwyg-print-designer`
- [ ] The branch name or PR description contains the applicable change id.
- [ ] Cross-module behavior is represented by an OpenSpec change.
- [ ] Completed behavior has corresponding tests and Markdown documentation.

## Verification

- [ ] `dotnet build Rmpp.sln -c Release`
- [ ] `dotnet test Rmpp.sln -c Release`
- [ ] `node .gitnexus/run.cjs analyze` and `detect_changes(scope: compare, base_ref: main)`
- [ ] Offline/privacy/licence/performance checks relevant to this change
- [ ] No customer, imported-row, template-asset, printer, or machine-sensitive data is attached

## Release gates

- [ ] Physical printer evidence is attached when print behavior changes.
- [ ] Installer/portable evidence is attached when packaging changes.
- [ ] Stable-release gates remain false until independent evidence exists.
