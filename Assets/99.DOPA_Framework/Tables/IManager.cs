using System.Threading.Tasks;

public interface IManager
{
    Task LoadDataAsync(System.Action<float> onProgressUpdated);
    Task LoadDataAsync();
}