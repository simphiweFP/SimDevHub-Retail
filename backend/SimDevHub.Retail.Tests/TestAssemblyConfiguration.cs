using Xunit;

// Shared CI PostgreSQL database is migrated by integration tests; avoid simultaneous schema creation.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
