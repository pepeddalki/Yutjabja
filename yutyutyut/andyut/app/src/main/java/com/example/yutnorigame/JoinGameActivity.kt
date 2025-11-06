package com.example.yutnorigame

import android.content.Intent
import android.os.Bundle
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity
import com.example.yutnorigame.databinding.ActivityJoinGameBinding
import com.google.firebase.database.DataSnapshot
import com.google.firebase.database.DatabaseError
import com.google.firebase.database.ValueEventListener
import com.google.firebase.database.ktx.database
import com.google.firebase.ktx.Firebase

class JoinGameActivity : AppCompatActivity() {

    private lateinit var binding: ActivityJoinGameBinding
    private lateinit var playerName: String // 내 이름 저장할 변수

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivityJoinGameBinding.inflate(layoutInflater)
        setContentView(binding.root)

        // 로비에서 전달받은 플레이어 이름 가져오기
        playerName = intent.getStringExtra("playerName") ?: "Player2"

        binding.joinButton.setOnClickListener {
            val inviteCode = binding.codeEditText.text.toString()

            if (inviteCode.length == 6) {
                joinRoom(inviteCode) // 함수 이름 변경
            } else {
                Toast.makeText(this, "6자리 초대코드를 입력해주세요.", Toast.LENGTH_SHORT).show()
            }
        }
        binding.cancelButton.setOnClickListener {
            finish()
        }
    }

    private fun joinRoom(inviteCode: String) {
        val database = Firebase.database.reference.child("rooms").child(inviteCode)

        database.addListenerForSingleValueEvent(object : ValueEventListener {
            override fun onDataChange(snapshot: DataSnapshot) {
                if (snapshot.exists()) {
                    // 방이 존재할 경우
                    val player2 = snapshot.child("player2").getValue(String::class.java)

                    if (player2.isNullOrEmpty()) {
                        // 참가자(player2) 자리가 비어있으면 참여 가능
                        database.child("player2").setValue(playerName).addOnSuccessListener {
                            // 성공적으로 참여했으면 대기방 화면으로 이동
                            Toast.makeText(this@JoinGameActivity, "게임에 참여합니다!", Toast.LENGTH_SHORT).show()

                            val player1Name = snapshot.child("player1").getValue(String::class.java)

                            val intent = Intent(this@JoinGameActivity, WaitingRoomActivity::class.java)
                            intent.putExtra("PLAYER_ROLE", "GUEST")
                            intent.putExtra("PLAYER_1_NAME", player1Name)
                            intent.putExtra("PLAYER_2_NAME", playerName)
                            intent.putExtra("ROOM_ID", inviteCode)
                            startActivity(intent)
                            finish()
                        }.addOnFailureListener {
                            Toast.makeText(this@JoinGameActivity, "참여에 실패했습니다.", Toast.LENGTH_SHORT).show()
                        }
                    } else {
                        // 자리가 비어있지 않으면 이미 다른 사람이 참여한 것
                        Toast.makeText(this@JoinGameActivity, "이미 꽉 찬 방입니다.", Toast.LENGTH_SHORT).show()
                    }
                } else {
                    // 방이 존재하지 않을 경우
                    Toast.makeText(this@JoinGameActivity, "존재하지 않는 방입니다.", Toast.LENGTH_SHORT).show()
                }
            }

            override fun onCancelled(error: DatabaseError) {
                Toast.makeText(this@JoinGameActivity, "오류가 발생했습니다: ${error.message}", Toast.LENGTH_SHORT).show()
            }
        })
    }
}
