public class CreateRoomRequest
{
    public string room_name { get; set; } = string.Empty;
    public bool is_private { get; set; }
    public string? password { get; set; }
    public int node_id { get; set; }
    public int max_users { get; set; } = 10;
}