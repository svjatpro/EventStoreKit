using NUnit.Framework;

// Run tests in parallel. InstancePerTestCase gives each test its own fixture instance, so the per-test
// Service / store fields stay isolated and method-level parallelism is safe.
[assembly: Parallelizable(ParallelScope.All)]
[assembly: FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
