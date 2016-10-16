using UnityEngine;
using System.Collections;

public class MountedRigPosition : MonoBehaviour
{
    public Camera camera;
    // Use this for initialization
    void Start()
    {
        transform.position = camera.transform.position;
        transform.rotation = camera.transform.rotation;
        //transform.localScale = new Vector3(3, 3, 3);
    }

    // Update is called once per frame
    void OnPreRender()
    {
        transform.position = camera.transform.position;
        transform.rotation = camera.transform.rotation;
        //transform.localScale = new Vector3(3, 3, 3);
    }
}
