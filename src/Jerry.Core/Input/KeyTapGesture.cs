namespace Jerry.Core.Input;
public sealed class KeyTapGesture
{
    private long started;
    private bool candidate;
    public void Begin(long time, bool otherKeysHeld) { started = time; candidate = !otherKeysHeld; }
    public void Invalidate() => candidate = false;
    public bool Release(long time)
    {
        var valid = candidate && time >= started && time - started <= 350;
        candidate = false; return valid;
    }
}
