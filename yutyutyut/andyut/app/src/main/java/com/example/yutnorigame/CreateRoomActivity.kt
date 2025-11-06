package com.example.yutnorigame

import android.content.Intent
import android.os.Bundle
import android.util.Log
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity
import com.example.yutnorigame.databinding.ActivityCreateRoomBinding
import com.google.firebase.database.DataSnapshot
import com.google.firebase.database.DatabaseError
import com.google.firebase.database.ValueEventListener
import com.google.firebase.database.ktx.database
import com.google.firebase.ktx.Firebase
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlin.random.Random

class CreateRoomActivity : AppCompatActivity() {

    private lateinit var binding: ActivityCreateRoomBinding
    private var waitingAnimationJob: Job? = null

    private lateinit var inviteCode: String
    private var roomEventListener: ValueEventListener? = null
    private var isRoomFilled = false // 방이 찼는지 확인하는 플래그

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivityCreateRoomBinding.inflate(layoutInflater)
        setContentView(binding.root)

        val playerName = intent.getStringExtra("playerName") ?: "Player1"

        inviteCode = Random.nextInt(100000, 1000000).toString()
        binding.inviteCodeTextView.text = inviteCode

        createRoomInFirebase(playerName)

        binding.cancelButton.setOnClickListener {
            finish()
        }
    }

    private fun createRoomInFirebase(playerName: String) {
        val database = Firebase.database.reference.child("rooms").child(inviteCode)

        // WaitingRoomActivity가 사용하는 데이터 구조에 맞게 방 정보를 생성합니다.
        val roomInfo = mapOf(
            "player1" to playerName,
            "player2" to "",
            "player2Status" to "waiting", // 참가자 초기 상태
            "gameStatus" to "waiting"    // 게임 초기 상태
        )

        database.setValue(roomInfo).addOnSuccessListener {
            addRoomEventListener(playerName)
        }.addOnFailureListener {
            Toast.makeText(this, "방 생성에 실패했습니다.", Toast.LENGTH_SHORT).show()
            finish()
        }
    }

    private fun addRoomEventListener(player1Name: String) {
        val database = Firebase.database.reference.child("rooms").child(inviteCode)

        roomEventListener = database.addValueEventListener(object : ValueEventListener {
            override fun onDataChange(snapshot: DataSnapshot) {
                val player2 = snapshot.child("player2").getValue(String::class.java)
                if (player2 != null && player2.isNotEmpty()) {
                    isRoomFilled = true
                    Toast.makeText(this@CreateRoomActivity, "$player2 님이 참가했습니다!", Toast.LENGTH_SHORT).show()

                    val intent = Intent(this@CreateRoomActivity, WaitingRoomActivity::class.java)
                    intent.putExtra("PLAYER_ROLE", "HOST")
                    intent.putExtra("PLAYER_1_NAME", player1Name)
                    intent.putExtra("PLAYER_2_NAME", player2)
                    intent.putExtra("ROOM_ID", inviteCode)
                    startActivity(intent)
                    finish()
                }
            }

            override fun onCancelled(error: DatabaseError) {
                Log.e("CreateRoomActivity", "데이터베이스 오류: ${error.message}")
            }
        })
    }

    override fun onResume() {
        super.onResume()
        startWaitingAnimation()
    }

    override fun onPause() {
        super.onPause()
        stopWaitingAnimation()
    }

    override fun onDestroy() {
        super.onDestroy()
        if (roomEventListener != null) {
            Firebase.database.reference.child("rooms").child(inviteCode).removeEventListener(roomEventListener!!)
        }
        if (!isRoomFilled) {
            Firebase.database.reference.child("rooms").child(inviteCode).removeValue()
        }
    }

    private fun startWaitingAnimation() {
        waitingAnimationJob?.cancel()
        waitingAnimationJob = CoroutineScope(Dispatchers.Main).launch {
            val baseText = "친구 기다리는 중"
            var dotCount = 0
            while (true) {
                when (dotCount) {
                    0 -> binding.waitingTextView.text = "$baseText."
                    1 -> binding.waitingTextView.text = "$baseText.."
                    2 -> binding.waitingTextView.text = "$baseText..."
                }
                dotCount = (dotCount + 1) % 3
                delay(500)
            }
        }
    }

    private fun stopWaitingAnimation() {
        waitingAnimationJob?.cancel()
        waitingAnimationJob = null
    }
}
