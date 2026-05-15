using ErrorOr;
using GymManagement.Domain.Rooms;


namespace GymManagement.Domain.Gyms;
public class Gym
{
    private readonly int _maxRooms;

    public Guid Id { get; }
    private readonly List<Guid> _roomIds = new();

    public string Name { get; init; } = null!;
    public Guid SubscriptionId { get; init; }

    public Gym(
        string name,
        int maxRooms,
        Guid subscriptionId,
        Guid? id = null)
    {
        Name = name;
        _maxRooms = maxRooms;
        SubscriptionId = subscriptionId;
        Id = id ?? Guid.NewGuid();
    }

    public bool HasRoom(Guid roomId)
    {
        return _roomIds.Contains(roomId);
    }

    public void RemoveRoom(Guid roomId)
    {
        _roomIds.Remove(roomId);
    }

    private Gym() { }
}
