public class PopupUpdate : PopupBase
{
    public override void Close()
    {
        VersionManager.Instance.OpenStorePage();
    }
}