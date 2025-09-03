using UnityEngine;

// CharacterControllerコンポーネントがアタッチされていることを保証
namespace _Project.Scripts.Character
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("プレイヤー設定")]
        public float moveSpeed = 5.0f; // 歩く速度
        public float gravity = -9.81f; // 重力

        [Header("カメラ設定")]
        public Camera playerCamera; // FPS視点用のカメラ
        public float mouseSensitivity = 2.0f; // マウス感度
        public float verticalLookLimit = 80.0f; // 上下の視点移動の制限角度

        // 内部変数
        private CharacterController controller;
        private Vector3 playerVelocity;
        private float xRotation = 0f;

        void Start()
        {
            // CharacterControllerコンポーネントを取得
            controller = GetComponent<CharacterController>();

            // マウスカーソルを画面中央にロックして非表示にする
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Update()
        {
            // プレイヤーの移動処理
            MovePlayer();

            // カメラの視点移動処理
            LookAround();
        }

        // プレイヤーの移動を処理するメソッド
        void MovePlayer()
        {
            // CharacterControllerが地面に設置しているか確認
            if (controller.isGrounded && playerVelocity.y < 0)
            {
                playerVelocity.y = -2f; // 地面に押し付ける небольшой力
            }

            // キーボードからの入力を取得 (W, A, S, Dキー)
            float x = Input.GetAxis("Horizontal");
            float z = Input.GetAxis("Vertical");

            // 入力に基づいて移動方向のベクトルを作成
            // transform.rightとtransform.forwardを使うことで、プレイヤーの向いている方向を基準に移動
            Vector3 move = transform.right * x + transform.forward * z;

            // CharacterControllerを使ってプレイヤーを移動させる
            controller.Move(move * moveSpeed * Time.deltaTime);

            // 重力を適用
            playerVelocity.y += gravity * Time.deltaTime;
            controller.Move(playerVelocity * Time.deltaTime);
        }

        // マウス入力による視点移動を処理するメソッド
        void LookAround()
        {
            // マウスのX軸（左右）とY軸（上下）の移動量を取得
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            // X軸の回転（左右の視点移動）はプレイヤーオブジェクト自体を回転させる
            transform.Rotate(Vector3.up * mouseX);

            // Y軸の回転（上下の視点移動）
            xRotation -= mouseY;
            // 視点移動の角度を制限する（例: -80度から80度まで）
            xRotation = Mathf.Clamp(xRotation, -verticalLookLimit, verticalLookLimit);

            // カメラのX軸の角度を更新
            // transform.localRotationを使うことで、プレイヤーの回転に影響されずにカメラだけを上下に向ける
            playerCamera.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }
    }
}