package com.example.yutnorigame

import android.content.Intent
import android.os.Bundle
import android.view.View
import android.widget.Button
import android.widget.TextView
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.ContextCompat
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

class WaitingRoomActivity : AppCompatActivity() {

    companion object {
        private const val UNITY_GAME_REQUEST_CODE = 1001
    }

    private lateinit var waitingRoomTitle: TextView
    private lateinit var player1NameTextView: TextView
    private lateinit var player2NameTextView: TextView
    private lateinit var player1StatusTextView: TextView
    private lateinit var player2StatusTextView: TextView
    private lateinit var gameStartButton: Button
    private lateinit var readyButton: Button
    private lateinit var exitButton: Button

    private var playerRole: String? = null
    private var roomId: String? = null
    private var roomEventListener: ValueEventListener? = null
    private val database = Firebase.database.reference

    private var waitingAnimationJob: Job? = null
    private var isPlayer2Ready = false
    private var isGameStarted = false

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_waiting_room)

        // UI 요소 초기화
        waitingRoomTitle = findViewById(R.id.waitingRoomTitle)
        player1NameTextView = findViewById(R.id.player1Name)
        player2NameTextView = findViewById(R.id.player2Name)
        player1StatusTextView = findViewById(R.id.player1Status)
        player2StatusTextView = findViewById(R.id.player2Status)
        gameStartButton = findViewById(R.id.gameStartButton)
        readyButton = findViewById(R.id.readyButton)
        exitButton = findViewById(R.id.exitButton)

        // Intent에서 데이터 가져오기
        playerRole = intent.getStringExtra("PLAYER_ROLE")
        roomId = intent.getStringExtra("ROOM_ID")
        val player1Name = intent.getStringExtra("PLAYER_1_NAME") ?: "Player 1"
        val player2Name = intent.getStringExtra("PLAYER_2_NAME") ?: "Player 2"

        player1NameTextView.text = player1Name
        player2NameTextView.text = player2Name

        setupUI()
        addRoomEventListener()

        readyButton.setOnClickListener {
            onReadyButtonClicked()
        }

        exitButton.setOnClickListener {
            onExitButtonClicked() // Use the dedicated exit function
        }

        gameStartButton.setOnClickListener {
            roomId?.let {
                database.child("rooms").child(it).child("gameStatus").setValue("started")
            }
        }
    }

    private fun setupUI() {
        if (playerRole == "HOST") {
            gameStartButton.visibility = View.VISIBLE
            readyButton.visibility = View.GONE
            gameStartButton.isEnabled = false // 초기에는 비활성화
        } else {
            gameStartButton.visibility = View.GONE
            readyButton.visibility = View.VISIBLE
        }
    }

    private fun onReadyButtonClicked() {
        isPlayer2Ready = !isPlayer2Ready
        val newStatus = if (isPlayer2Ready) "ready" else "waiting"
        roomId?.let {
            database.child("rooms").child(it).child("player2Status").setValue(newStatus)
        }
    }

    private fun onExitButtonClicked() {
        roomId?.let { id ->
            database.child("rooms").child(id).removeValue()
        }
        removeListenerAndGoToLobby()
    }

    private fun removeListenerAndGoToLobby() {
        if (roomEventListener != null) {
            roomId?.let {
                database.child("rooms").child(it).removeEventListener(roomEventListener!!)
            }
        }
        val intent = Intent(this, LobbyActivity::class.java)
        intent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP or Intent.FLAG_ACTIVITY_SINGLE_TOP)
        startActivity(intent)
        finish()
    }

    private fun addRoomEventListener() {
        roomId?.let {
            roomEventListener = database.child("rooms").child(it).addValueEventListener(object : ValueEventListener {
                override fun onDataChange(snapshot: DataSnapshot) {
                    if (!snapshot.exists()) {
                        removeListenerAndGoToLobby()
                        return
                    }

                    val gameStatus = snapshot.child("gameStatus").getValue(String::class.java)
                    if (gameStatus == "started") {
                        isGameStarted = true
                        
                        // 게임 세션 생성 및 유니티 실행
                        createGameSessionAndStartUnity()
                        return
                    }

                    val player2Status = snapshot.child("player2Status").getValue(String::class.java)
                    isPlayer2Ready = player2Status == "ready"

                    updatePlayer2StatusUI()

                    if (playerRole == "HOST") {
                        gameStartButton.isEnabled = isPlayer2Ready
                    }
                }

                override fun onCancelled(error: DatabaseError) {
                    // Handle error
                }
            })
        }
    }

    private fun updatePlayer2StatusUI() {
        if (isPlayer2Ready) {
            player2StatusTextView.text = "준비완료"
            player2StatusTextView.setTextColor(ContextCompat.getColor(this, R.color.status_ready))
            if (playerRole == "GUEST") {
                readyButton.text = "준비완료!"
                exitButton.isEnabled = false
            }
        } else {
            player2StatusTextView.text = "대기 중..."
            player2StatusTextView.setTextColor(ContextCompat.getColor(this, R.color.status_waiting))
            if (playerRole == "GUEST") {
                readyButton.text = "준비"
                exitButton.isEnabled = true
            }
        }
    }

    private fun startWaitingAnimation() {
        waitingAnimationJob?.cancel()
        waitingAnimationJob = CoroutineScope(Dispatchers.Main).launch {
            val baseText = "Waiting"
            var dotCount = 0
            while (true) {
                when (dotCount) {
                    0 -> waitingRoomTitle.text = "$baseText."
                    1 -> waitingRoomTitle.text = "$baseText.."
                    2 -> waitingRoomTitle.text = "$baseText..."
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

    override fun onResume() {
        super.onResume()
        startWaitingAnimation()
    }

    override fun onPause() {
        super.onPause()
        stopWaitingAnimation()
    }

    private fun createGameSessionAndStartUnity() {
        roomId?.let { id ->
            // 게임 세션 ID 생성 (현재 시간 기반)
            val gameSessionId = "game_session_${System.currentTimeMillis()}"
            
            // 게임 세션 데이터 생성
            val gameSessionData = mapOf(
                "sessionId" to gameSessionId,
                "player1Id" to "android_player_1",
                "player2Id" to "android_player_2", 
                "player1Name" to player1NameTextView.text.toString(),
                "player2Name" to player2NameTextView.text.toString(),
                "gameStatus" to "playing",
                "createdTime" to System.currentTimeMillis(),
                "currentTurn" to "player1",
                "player1Score" to 0,
                "player2Score" to 0,
                "roomId" to id // 원본 방 ID 참조
            )
            
            // Firebase에 게임 세션 저장
            database.child("game_sessions").child(gameSessionId).setValue(gameSessionData)
                .addOnSuccessListener {
                    // 게임 세션 생성 성공 시 유니티 실행
                    startUnityGame(gameSessionId)
                }
                .addOnFailureListener { exception ->
                    Toast.makeText(this, "게임 세션 생성 실패: ${exception.message}", Toast.LENGTH_SHORT).show()
                }
        }
    }
    
    private fun startUnityGame(sessionId: String) {
        try {
            // Unity Library를 사용한 Unity Activity 실행
            val unityIntent = Intent(this, UnityGameActivity::class.java).apply {
                // 게임 세션 ID 전달
                putExtra("GAME_SESSION_ID", sessionId)
                putExtra("PLAYER_ROLE", if (playerRole == "HOST") "player1" else "player2")
                
                // 플레이어 이름들도 전달
                putExtra("PLAYER1_NAME", player1NameTextView.text.toString())
                putExtra("PLAYER2_NAME", player2NameTextView.text.toString())
            }
            
            // Unity Activity 시작 (결과를 받기 위해 startActivityForResult 사용)
            startActivityForResult(unityIntent, UNITY_GAME_REQUEST_CODE)
            
            val roleText = if (playerRole == "HOST") "방장" else "플레이어"
            Toast.makeText(this, "Unity as a Library 게임 시작 ($roleText): $sessionId", Toast.LENGTH_SHORT).show()
            
        } catch (e: Exception) {
            Toast.makeText(this, "Unity 게임 실행 실패: ${e.message}", Toast.LENGTH_LONG).show()
            
            // Unity Library가 없는 경우 대체 방법
            showUnityNotFoundDialog(sessionId)
        }
    }
    
    private fun showUnityNotFoundDialog(sessionId: String) {
        androidx.appcompat.app.AlertDialog.Builder(this)
            .setTitle("유니티 게임 앱 필요")
            .setMessage("게임을 플레이하려면 유니티 게임 앱이 필요합니다.\n\n게임 세션 ID: $sessionId\n\n이 ID를 유니티에서 사용하세요.")
            .setPositiveButton("확인") { _, _ ->
                // 게임 세션 ID를 클립보드에 복사
                val clipboard = getSystemService(android.content.Context.CLIPBOARD_SERVICE) as android.content.ClipboardManager
                val clip = android.content.ClipData.newPlainText("Game Session ID", sessionId)
                clipboard.setPrimaryClip(clip)
                
                Toast.makeText(this, "세션 ID가 클립보드에 복사되었습니다", Toast.LENGTH_SHORT).show()
            }
            .setNegativeButton("로비로 돌아가기") { _, _ ->
                removeListenerAndGoToLobby()
            }
            .setCancelable(false)
            .show()
    }

    override fun onActivityResult(requestCode: Int, resultCode: Int, data: Intent?) {
        super.onActivityResult(requestCode, resultCode, data)
        
        if (requestCode == UNITY_GAME_REQUEST_CODE) {
            when (resultCode) {
                RESULT_OK -> {
                    // Unity 게임이 정상적으로 종료됨
                    val winner = data?.getStringExtra("GAME_RESULT")
                    val sessionId = data?.getStringExtra("GAME_SESSION_ID")
                    
                    Toast.makeText(this, "게임 완료! 승자: $winner", Toast.LENGTH_LONG).show()
                    
                    // 게임 결과를 Firebase에 저장하거나 로비로 이동
                    removeListenerAndGoToLobby()
                }
                RESULT_CANCELED -> {
                    // Unity 게임이 취소됨 (뒤로가기 등)
                    Toast.makeText(this, "게임이 취소되었습니다", Toast.LENGTH_SHORT).show()
                }
            }
        }
    }

    override fun onDestroy() {
        super.onDestroy()
        if (roomEventListener != null) {
            roomId?.let {
                database.child("rooms").child(it).removeEventListener(roomEventListener!!)
            }
        }
        if (!isGameStarted) {
            roomId?.let {
                database.child("rooms").child(it).removeValue()
            }
        }
    }
}
