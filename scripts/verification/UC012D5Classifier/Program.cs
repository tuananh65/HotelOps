using Microsoft.EntityFrameworkCore;
using UC012D5Classifier;

if (args is not ["--self-test"])
{
    Console.WriteLine("ISOLATED_SOURCE_CANDIDATE; NO_PROVIDER_PROFILE_APPROVED; --self-test only");
    return 2;
}
var passed = 0;
void Equal(Classification value, FailureKind kind, int? status, string code = "")
{
    if (value.Kind != kind || value.HttpStatus != status || value.PublicCode != code)
        throw new InvalidOperationException("Classification mismatch");
    passed++;
}
try
{
    Equal(StorageFailureClassifier.Classify(new Exception("private SQL/token details")), FailureKind.Unexpected, 500, "INTERNAL_SERVER_ERROR");
    Equal(StorageFailureClassifier.Classify(new DbUpdateException("40501 temporarily busy")), FailureKind.Unexpected, 500, "INTERNAL_SERVER_ERROR");
    Equal(StorageFailureClassifier.Classify(new DbUpdateException("outer", new InvalidOperationException("40501"))), FailureKind.Unexpected, 500, "INTERNAL_SERVER_ERROR");
    Equal(StorageFailureClassifier.Classify(new DbUpdateException("outer", new DbUpdateException("inner"))), FailureKind.Unexpected, 500, "INTERNAL_SERVER_ERROR");
    Equal(StorageFailureClassifier.Classify(new OperationCanceledException()), FailureKind.Cancelled, null);
    Equal(StorageFailureClassifier.Classify(new DbUpdateException("outer", new OperationCanceledException())), FailureKind.Cancelled, null);
    Equal(StorageFailureClassifier.Classify(new DbUpdateConcurrencyException()), FailureKind.OperationOwned, null);
    Equal(StorageFailureClassifier.Classify(new DbUpdateException("outer", new DbUpdateConcurrencyException())), FailureKind.OperationOwned, null);
    Equal(StorageFailureClassifier.Classify(new AggregateException(new DbUpdateException(), new OperationCanceledException())), FailureKind.Unexpected, 500, "INTERNAL_SERVER_ERROR");
    Exception deep = new OperationCanceledException();
    for (var i = 0; i < 10; i++) deep = new DbUpdateException("wrapper", deep);
    Equal(StorageFailureClassifier.Classify(deep), FailureKind.Unexpected, 500, "INTERNAL_SERVER_ERROR");

    // This profile is an OFFLINE TEST VECTOR. 40501 is still a proposed number.
    var synthetic = new StorageProfile("OFFLINE_ONLY_NOT_APPROVED", [40501]);
    Equal(StorageFailureClassifier.ClassifyProviderNumbers([40501], synthetic), FailureKind.ClassifiedStorage, 503, "BRANCH_STORE_UNAVAILABLE");
    Equal(StorageFailureClassifier.ClassifyProviderNumbers([40501, 40501], synthetic), FailureKind.ClassifiedStorage, 503, "BRANCH_STORE_UNAVAILABLE");
    foreach (var numbers in new int[][] { [], [40501], [40501, 208], [208], [0], [-1], [40501, 2601], [40501, 2627], [40501, 0] })
    {
        var profile = numbers.SequenceEqual(new[] { 40501 }) ? StorageProfile.Disabled : synthetic;
        Equal(StorageFailureClassifier.ClassifyProviderNumbers(numbers, profile), FailureKind.Unexpected, 500, "INTERNAL_SERVER_ERROR");
    }
    foreach (var numbers in new int[][] { [2601], [2627], [2601, 2627] })
        Equal(StorageFailureClassifier.ClassifyProviderNumbers(numbers, synthetic), FailureKind.OperationOwned, null);
    foreach (var numbers in new int[][] { [1205], [-2], [40501, 1205], [40501, -2] })
        Equal(StorageFailureClassifier.ClassifyProviderNumbers(numbers, synthetic), FailureKind.OutcomeUnconfirmed, null);
    Equal(StorageFailureClassifier.ClassifyProviderNumbers(Enumerable.Repeat(40501, 65).ToArray(), synthetic), FailureKind.Unexpected, 500, "INTERNAL_SERVER_ERROR");
    foreach (var forbidden in new[] { 0, -2, 1205, 2601, 2627 })
    {
        try { _ = new StorageProfile("test", [forbidden]); throw new InvalidOperationException("Profile accepted forbidden number"); }
        catch (ArgumentException) { passed++; }
    }
    var mutable = new[] { 40501 };
    var frozen = new StorageProfile("offline", mutable); mutable[0] = 208;
    Equal(StorageFailureClassifier.ClassifyProviderNumbers([208], frozen), FailureKind.Unexpected, 500, "INTERNAL_SERVER_ERROR");
    Equal(StorageFailureClassifier.ClassifyProviderNumbers([40501], frozen), FailureKind.ClassifiedStorage, 503, "BRANCH_STORE_UNAVAILABLE");
    Console.WriteLine($"{passed}/{passed} OFFLINE CHECKS PASS; PROVIDER_INJECTION_SQL_HTTPS_ROLLBACK_NOT_RUN");
    return 0;
}
catch { Console.WriteLine($"OFFLINE CHECK FAILED AFTER {passed}; NO_EXCEPTION_DETAIL_LOGGED"); return 1; }
