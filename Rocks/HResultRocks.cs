using ClrDebug;

namespace SpaceEditor.Rocks;

public static class HResultRocks
{
    public static bool IsOk(this HRESULT result)
    {
        return result == HRESULT.S_OK;
    }

    public static bool IsFail(this HRESULT result)
    {
        return result.IsOk() == false;
    }
}