using UnityEngine;

public class MirrorFloor : MonoBehaviour
{
    ReflectionProbe rp;

    void Start()
    {
        // Reflection Probeを取得
        rp = GetComponent<ReflectionProbe>();
    }

    void Update()
    {
        // Reflection Probeをカメラの位置に応じて移動
        rp.transform.position = new Vector3(
            Camera.main.transform.position.x,
            Camera.main.transform.position.y * -1,
            Camera.main.transform.position.z
        );

        // Reflection Probeを更新
        rp.RenderProbe();
    }
}