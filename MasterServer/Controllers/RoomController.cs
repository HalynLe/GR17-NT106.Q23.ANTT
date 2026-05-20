using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;

[Authorize]
[ApiController]
[Route("api/room")]
public class RoomController : ControllerBase
{
    private readonly RoomService _roomService;
    private readonly NodeService _nodeService;

    public RoomController(RoomService roomService, NodeService nodeService)
    {
        _roomService = roomService;
        _nodeService = nodeService;
    }

    private int GetUserId()
    {
        var claim = User.FindFirst("user_id");

        if (claim == null)
        {
            Console.WriteLine("[API ROOM] CẢNH BÁO: Không tìm thấy user_id trong Token!");
            throw new Exception("Unauthorized");
        }

        return int.Parse(claim.Value);
    }

    private NodeInfo GetBestNode()
    {
        var activeNode = _nodeService.GetAnyActiveNode();
        if (activeNode == null) return null;

        return new NodeInfo
        {
            Ip = activeNode.ip_address,
            Port = activeNode.port,
            MaxUsers = 100,
            CurrentUsers = 0
        };
    }

    // --- ENDPOINT MỚI CHO NODE SERVER ---
    [AllowAnonymous] // Cho phép Node Server gọi báo cáo trạng thái mà không cần JWT Token
    [HttpPost("update-status")]
    public IActionResult UpdateStatus([FromBody] UserStatusUpdateDto req)
    {
        try
        {
            _roomService.UpdateUserStatus(req.user_id, req.room_id, req.is_online);
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"[MASTER - ROOM] Cập nhật trạng thái DB: User ID {req.user_id} -> Phòng {req.room_id} -> {(req.is_online == 1 ? "ONLINE" : "OFFLINE")}");
            Console.ResetColor();
            return Ok();
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("create")]
    public IActionResult CreateRoom(CreateRoomRequest req)
    {
        try
        {
            int userId = GetUserId();
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"[MASTER - ROOM] User ID {userId} yêu cầu tạo phòng vẽ mới: '{req.room_name}'");
            Console.ResetColor();
            var room = _roomService.CreateRoom(req, userId);

            // ĐIỀU HƯỚNG TỚI NODE
            var node = GetBestNode();
            if (node == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[MASTER - LOAD BALANCER] THẤT BẠI: Từ chối tạo phòng của User {userId} do không tìm thấy Node khả dụng.");
                Console.ResetColor();
                return BadRequest(new { message = "Hệ thống máy chủ vẽ đang quá tải!" });
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[MASTER SERVER] Tạo phòng thành công. Điều hướng User {userId} sang Node [{node.Ip}:{node.Port}]");
            Console.ResetColor();

            // Gộp thông tin phòng và cấu hình Node trả về cho WPF
            return Ok(new
            {
                roomInfo = room,
                nodeIp = node.Ip,
                nodePort = node.Port
            });
        }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("join")]
    public IActionResult JoinRoom(JoinRoomRequest req)
    {
        try
        {
            int userId = GetUserId();
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"[MASTER - ROOM] User ID {userId} gửi yêu cầu xin tham gia vào phòng ID: {req.room_id}");
            Console.ResetColor();

            var room = _roomService.JoinRoom(req, userId);
            if (room == null) return NotFound(new { message = "Room not found" });

            // ĐIỀU HƯỚNG TỚI NODE
            var node = GetBestNode();
            if (node == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[MASTER SERVER - LOAD BALANCER] THẤT BẠI: Từ chối điều hướng User {userId} do không tìm thấy Node khả dụng.");
                Console.ResetColor();

                return BadRequest(new { message = "Hệ thống máy chủ vẽ đang quá tải!" });
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[MASTER SERVER - LOAD BALANCER] ĐIỀU HƯỚNG THÀNH CÔNG: User {userId} -> Node [{node.Ip}:{node.Port}]");
            Console.ResetColor();

            return Ok(new
            {
                roomInfo = room,
                nodeIp = node.Ip,
                nodePort = node.Port
            });
        }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("leave")]
    public IActionResult LeaveRoom(JoinRoomRequest req)
    {
        try
        {
            int userId = GetUserId();
            _roomService.LeaveRoom(req.room_id, userId);
            
            Console.ForegroundColor = ConsoleColor.Magenta; 
            Console.WriteLine($"[MASTER - ROOM] User ID {userId} đã ngắt kết nối và rời phòng {req.room_id}.");
            Console.ResetColor();
             
            return Ok();
        }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("list")]
    public IActionResult GetRooms()
    {
        try
        {
            return Ok(_roomService.GetRooms());
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{roomId}/members")]
    public IActionResult GetMembers(int roomId)
    {
        return Ok(_roomService.GetMembers(roomId));
    }
}

// Data Transfer Object cho Node Server
public class NodeInfo
{
    public string Ip { get; set; } = string.Empty;
    public int Port { get; set; }
    public int MaxUsers { get; set; }
    public int CurrentUsers { get; set; }
}

public class UserStatusUpdateDto
{
    public int user_id { get; set; }
    public int room_id { get; set; }
    public int is_online { get; set; } // 1: online, 0: offline
}