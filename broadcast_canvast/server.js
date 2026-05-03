const io = require("socket.io")(3000, {
  cors: { origin: "*" }
});

console.log("Server running at http://localhost:3000");

io.on("connection", (socket) => {
  console.log("User connected");

  // join room
  socket.on("join_room", (room_id) => {
    socket.join(room_id);
  });

  // nhận draw → broadcast lại
  socket.on("draw", (msg) => {
    socket.to(msg.room_id).emit("draw", msg);
  });

  socket.on("disconnect", () => {
    console.log("User disconnected");
  });
});