using UnityEngine;
using UnityEngine.Splines;

public class NaveSplineFollow : MonoBehaviour
{
    public SplineContainer spline;
    [Range(0f, 0.2f)]
    public float speed = 0.05f;

    // Evento al terminar
    public System.Action OnSplineTerminado;

    private float t = 0f;
    private bool terminado = false;

    void FixedUpdate()
    {
        if (terminado) return;

        t += speed * Time.fixedDeltaTime;

        if (t >= 1f)
        {
            t = 1f;
            terminado = true;
            // Notifica que termino
            OnSplineTerminado?.Invoke();
            return;
        }

        Vector3 pos = spline.transform.TransformPoint(
            spline.Spline.EvaluatePosition(t)
        );
        Vector3 tangent = spline.transform.TransformDirection(
            (Vector3)spline.Spline.EvaluateTangent(t)
        );

        transform.position = pos;
        if (tangent != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(tangent);
    }
}