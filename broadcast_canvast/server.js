const io = require("socket.io")(3000, {
  cors: { origin: "*" }
});

console.log("Server running at http://localhost:3000");

io.on("connection", (socket) => {

  socket.on("join_room", (room_id) => {
    socket.join(room_id);
  });

  socket.on("draw", (msg) => {
    // broadcast cho người khác trong room
    socket.to(msg.room_id).emit("draw", msg);
  });

});
