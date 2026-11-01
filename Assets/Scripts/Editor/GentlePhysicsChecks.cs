using System;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GentlePhysicsChecks
{
    public static string Validate()
    {
        Scene testScene = EditorSceneManager.NewPreviewScene();
        try
        {
            PhysicsScene physics = testScene.GetPhysicsScene();
            if (!physics.IsValid() || physics == Physics.defaultPhysicsScene)
                return "SKIPPED: no isolated editor physics scene available; gameplay test still required.";
            GameObject floor = new GameObject("Test floor");
            SceneManager.MoveGameObjectToScene(floor, testScene);
            floor.transform.position = new Vector3(0f, -0.05f, 0f);
            floor.AddComponent<BoxCollider>().size = new Vector3(2f, 0.1f, 2f);
            Rigidbody[] fruit = new Rigidbody[4];
            for (int i = 0; i < fruit.Length; i++)
            {
                GameObject item = new GameObject("Test fruit");
                SceneManager.MoveGameObjectToScene(item, testScene);
                item.transform.position = new Vector3(0f, 0.4f + i * 0.18f, 0f);
                item.AddComponent<SphereCollider>().radius = 0.07f;
                fruit[i] = item.AddComponent<Rigidbody>();
                fruit[i].mass = 0.15f;
                GentleItemPhysics.Configure(fruit[i], false);
                if (fruit[i].isKinematic || !fruit[i].useGravity || fruit[i].constraints != RigidbodyConstraints.None)
                    throw new Exception("Fruit physics was constrained.");
                if (item.GetComponent<Collider>().sharedMaterial == null)
                    throw new Exception("Contact material missing.");
            }
            for (int frame = 0; frame < 300; frame++) physics.Simulate(0.02f);
            foreach (Rigidbody item in fruit)
            {
                if (item.position.y < 0.04f || item.position.y > 0.6f || item.linearVelocity.magnitude > 0.25f)
                    throw new Exception("Drop/settling check failed: " + item.position + " speed=" + item.linearVelocity.magnitude);
            }
            // A small deliberate overlap must separate without a violent impulse.
            fruit[0].position = new Vector3(-0.06f, 1f, 0f);
            fruit[1].position = new Vector3(0.06f, 1f, 0f);
            fruit[0].WakeUp(); fruit[1].WakeUp();
            float maximumSidewaysSpeed = 0f;
            for (int frame = 0; frame < 20; frame++)
            {
                physics.Simulate(0.02f);
                maximumSidewaysSpeed = Mathf.Max(maximumSidewaysSpeed,
                    Mathf.Abs(fruit[0].linearVelocity.x), Mathf.Abs(fruit[1].linearVelocity.x));
            }
            if (maximumSidewaysSpeed > 1.1f) throw new Exception("Overlap correction too fast: " + maximumSidewaysSpeed);
            return "PASS: four free dynamic fruit fall and settle; overlap sideways speed=" + maximumSidewaysSpeed;
        }
        finally { EditorSceneManager.ClosePreviewScene(testScene); }
    }
}
