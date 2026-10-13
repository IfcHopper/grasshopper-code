# Public test fixtures

External test models come only from [buildingSMART/IFC4.x-specification-models](https://github.com/buildingSMART/IFC4.x-specification-models). `manifest.json` pins their revision, paths and SHA-256 hashes. Fetch before running tests:

```powershell
powershell -File tools/FetchTestFixtures.ps1
dotnet test IfcHopper.sln
```

The ignored `cache/` holds unchanged upstream bytes; no private models are required, discovered or uploaded. Missing fixtures fail with setup instructions. Tests run offline after acquisition; CI must run the fetch step first. Review upstream content and hashes when updating the manifest.

If Windows blocks scripts, review the script and use `powershell -ExecutionPolicy Bypass -File tools/FetchTestFixtures.ps1` for that process only; no permanent execution-policy change is needed.

Copyright buildingSMART International Ltd. Upstream states: all rights reserved until formal release; final IFC is licensed CC BY-ND 4.0. See the [upstream README at the pinned revision](https://github.com/buildingSMART/IFC4.x-specification-models/blob/50e6c5c63a3586badc5dc857f5f4736675135525/README.md). This project does not relicense or redistribute the downloaded models. Keep attribution and upstream notices with any permitted redistribution; test output is disposable, not a published derivative fixture.

Existing synthetic unit tests and project-generated `samples/ifc/` tests remain independent of external models. Public examples replace private whole-model checks, not their exact entity counts or project-specific assumptions.
