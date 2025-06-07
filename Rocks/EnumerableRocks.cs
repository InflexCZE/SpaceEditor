namespace SpaceEditor.Rocks;

public static class EnumerableRocks
{
    public static IEnumerable<(T Element, bool First, bool Last)> WithFirstAndLast<T>
    (
        this IEnumerable<T> sequence,
        bool throwWhenEmpty = true
    )
    {
        using var enumerator = sequence.GetEnumerator();

        if (enumerator.MoveNext() == false)
        {
            if (throwWhenEmpty)
            {
                throw new Exception("Sequence is empty");
            }

            yield break;
        }

        bool first = true;
        var current = enumerator.Current;

        while (enumerator.MoveNext())
        {
            yield return (current, first, Last: false);

            first = false;
            current = enumerator.Current;
        }
        
        yield return (current, first, Last: true);
    }

}