package com.example.yutnorigame

import android.content.Intent
import android.os.Bundle
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity
// 올바른 클래스 이름으로 수정합니다.
import com.unity3d.player.UnityPlayerGameActivity

/**
 * Unity 게임을 실행하는 Activity
 * Unity가 제공하는 UnityPlayerGameActivity를 실행합니다.
 */
class UnityGameActivity : AppCompatActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        // UnityPlayerGameActivity를 실행하기 위한 Intent 생성
        // 올바른 클래스 이름으로 수정합니다.
        val intent = Intent(this, UnityPlayerGameActivity::class.java)

        // 기존에 전달하던 데이터들을 Intent에 추가
        val gameSessionId = getIntent().getStringExtra("GAME_SESSION_ID")
        val playerRole = getIntent().getStringExtra("PLAYER_ROLE")
        val player1Name = getIntent().getStringExtra("PLAYER1_NAME")
        val player2Name = getIntent().getStringExtra("PLAYER2_NAME")

        intent.putExtra("GAME_SESSION_ID", gameSessionId)
        intent.putExtra("PLAYER_ROLE", playerRole)
        intent.putExtra("PLAYER1_NAME", player1Name)
        intent.putExtra("PLAYER2_NAME", player2Name)

        Toast.makeText(this, "Unity 게임 실행: $gameSessionId", Toast.LENGTH_SHORT).show()

        // UnityPlayerGameActivity 실행
        startActivity(intent)

        // 현재 Activity는 즉시 종료
        finish()
    }
}
