using Xunit;

// Several tests start the API with database settings; run the whole suite one test class at a time so they cannot interfere.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
