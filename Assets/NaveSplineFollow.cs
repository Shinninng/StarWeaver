using UnityEngine;
using UnityEngine.Splines;

public class NaveSplineFollow : MonoBehaviour
{
    public SplineContainer spline;
    [Range(0f, 0.2f)]
    public float speed = 0.05f;

    private float t = 0f;

    void FixedUpdate()
    {
        t += speed * Time.fixedDeltaTime;
        t = Mathf.Repeat(t, 1f);

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