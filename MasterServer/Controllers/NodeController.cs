using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/node")]
public class NodeController : ControllerBase
{
    private readonly NodeService _nodeService;

    public NodeController(NodeService nodeService)
    {
        _nodeService = nodeService;
    }

    [HttpPost("register")]
    public IActionResult Register(RegisterNodeRequest req)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[MASTER - NODE MGR] Nhận tín hiệu đăng ký tự động từ Node Server -> [{req.ip_address}:{req.port}]");
        Console.ResetColor();
        var nodeId = _nodeService.RegisterNode(req);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[MASTER - NODE MGR] TỰ ĐỘNG ĐĂNG KÝ THÀNH CÔNG -> Đã ghi nhận Node ID: {nodeId} | Trạng thái: ACTIVE");
        Console.ResetColor();
        return Ok(new { node_id = nodeId });
    }

    [HttpPost("heartbeat")]
    public IActionResult Heartbeat(HeartbeatRequest req)
    {
        var ok = _nodeService.UpdateHeartbeat(req.node_id);

        if (!ok)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[MASTER - HEARTBEAT] LỖI: Nhận tín hiệu từ Node ID {req.node_id} nhưng không tồn tại trong DB!");
            Console.ResetColor();
            return NotFound(new { message = "Node not found" });
        }

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"[MASTER - HEARTBEAT] Node ID {req.node_id} báo cáo: Đang hoạt động ổn định (ACTIVE).");
        Console.ResetColor();

        return Ok();
    }

    [HttpGet("list")]
    public IActionResult GetNodes()
    {
        return Ok(_nodeService.GetAllNodes());
    }
}

