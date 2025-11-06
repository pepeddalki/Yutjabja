package com.example.yutnorigame

import android.content.Intent // Intent import 확인
import android.content.pm.ActivityInfo
import android.os.Bundle
import androidx.appcompat.app.AppCompatActivity
import com.example.yutnorigame.databinding.ActivityLobbyBinding

class LobbyActivity : AppCompatActivity() {

    private lateinit var binding: ActivityLobbyBinding

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivityLobbyBinding.inflate(layoutInflater)
        setContentView(binding.root)

        val playerName = intent.getStringExtra("playerName")
        binding.playerNameTextView.text = playerName

        // '방 만들기' 버튼 클릭 시 CreateRoomActivity로 이동
        binding.createRoomButton.setOnClickListener {
            val intent = Intent(this, CreateRoomActivity::class.java)
            intent.putExtra("playerName", binding.playerNameTextView.text.toString())
            startActivity(intent)
        }

        binding.joinGameButton.setOnClickListener {
            val intent = Intent(this, JoinGameActivity::class.java)
            intent.putExtra("playerName", binding.playerNameTextView.text.toString())
            startActivity(intent)
        }
    }
}
