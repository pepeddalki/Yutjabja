package com.example.yutnorigame

import android.content.Intent
import android.content.pm.ActivityInfo
import android.os.Bundle
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity
import com.example.yutnorigame.databinding.ActivityMainBinding
import com.google.firebase.database.DataSnapshot
import com.google.firebase.database.DatabaseError
import com.google.firebase.database.ValueEventListener
import com.google.firebase.database.ktx.database
import com.google.firebase.ktx.Firebase

class MainActivity : AppCompatActivity() {

    private lateinit var binding: ActivityMainBinding

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        // 가로 모드 고정 (기존 코드 유지)
        requestedOrientation = ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE

        binding = ActivityMainBinding.inflate(layoutInflater)
        setContentView(binding.root)

        // XML의 startButton을 confirmButton으로 변경했다고 가정하고 코드를 수정합니다.
        // 만약 ID가 다르다면 이 부분을 실제 ID에 맞게 바꿔주세요. (예: binding.startButton)
        binding.confirmButton.setOnClickListener {
            val playerName = binding.nameEditText.text.toString().trim()

            if (playerName.isNotBlank()) {
                // 이름이 비어있지 않으면 중복 확인 함수 호출
                checkPlayerName(playerName)
            } else {
                Toast.makeText(this, "이름을 입력해주세요!", Toast.LENGTH_SHORT).show()
            }
        }
    }

    private fun checkPlayerName(playerName: String) {
        // Firebase 실시간 데이터베이스의 'users' 경로를 참조합니다.
        val database = Firebase.database.reference.child("users")

        // 'users' 경로에서 입력된 이름과 동일한 자식 노드가 있는지 확인합니다.
        database.child(playerName).addListenerForSingleValueEvent(object : ValueEventListener {
            override fun onDataChange(snapshot: DataSnapshot) {
                if (snapshot.exists()) {
                    // 스냅샷이 존재한다면 -> 이미 사용 중인 이름입니다.
                    Toast.makeText(this@MainActivity, "이미 사용 중인 이름입니다.", Toast.LENGTH_SHORT).show()
                } else {
                    // 스냅샷이 존재하지 않는다면 -> 사용 가능한 이름입니다.
                    // 1. 데이터베이스에 이름을 등록합니다. (추후 랭킹 등을 위해 등록)
                    database.child(playerName).setValue(true).addOnSuccessListener {
                        // 2. 등록 성공 시 로비로 이동합니다.
                        Toast.makeText(this@MainActivity, "'$playerName'님 환영합니다!", Toast.LENGTH_SHORT).show()
                        goToLobby(playerName)
                    }.addOnFailureListener {
                        // 등록 실패 시 오류 메시지 표시
                        Toast.makeText(this@MainActivity, "오류가 발생했습니다. 다시 시도해주세요.", Toast.LENGTH_SHORT).show()
                    }
                }
            }

            override fun onCancelled(error: DatabaseError) {
                // 데이터베이스 조회에 실패한 경우
                Toast.makeText(this@MainActivity, "데이터베이스 연결 오류가 발생했습니다.", Toast.LENGTH_SHORT).show()
            }
        })
    }

    private fun goToLobby(playerName: String) {
        val intent = Intent(this, LobbyActivity::class.java)
        intent.putExtra("playerName", playerName)
        startActivity(intent)
        finish() // 현재 화면을 닫아서 뒤로가기로 돌아오지 못하게 함
    }
}
