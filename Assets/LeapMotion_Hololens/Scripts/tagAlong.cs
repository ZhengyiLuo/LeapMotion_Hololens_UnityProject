using UnityEngine;
using System.Collections;
using Leap;

public class tagAlong : MonoBehaviour
{

    public LeapWebProcessor processor;
    public Material tex_red;
    public Material tex_green;

    // Use this for initialization
    void Start()
    {
        processor = FindObjectOfType<LeapWebProcessor>();
    }

    // Update is called once per frame
    void Update()
    {
        if (processor != null)
        {
            if (processor.hasHand)
            {
                GetComponent<Renderer>().material = tex_green;
            }
            else
            {
                GetComponent<Renderer>().material = tex_red;
            }
        }
        else
        {
            processor = FindObjectOfType<LeapWebProcessor>();
        }
    }
}
