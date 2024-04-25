using System.Collections.Generic;
using UnityEngine;
using Dreamteck.Splines;

public class ExampleJunctionHandler : MonoBehaviour
{
    SplineTracer tracer;

    private void Awake()
    {
        tracer = GetComponent<SplineTracer>();
    }

    private void OnEnable()
    {
        tracer.onNode += OnNode; //onNode is called every time the tracer passes by a Node
    }

    private void OnDisable()
    {
        tracer.onNode -= OnNode;
    }

    private void OnNode(List<SplineTracer.NodeConnection> passed)
    {
        //plineTracer.NodeConnection holds information about the point index and provides a direct reference to the Node
        //a list of Node connections is passed in case the tracer has moved over several points in the last frame
        //this happens very rarely in particular cases where points are very close to each other and the tracer moves very fast

        Debug.Log("Reached node " + passed[0].node.name + " connected at point " + passed[0].point);

        //Get all available connected splines

        Node.Connection[] connections = passed[0].node.GetConnections();

        //If this node does not have other connected splines, skip everything - there is no junction

        if (connections.Length == 1) return;

        //get the connected splines and find the index of the tracer's current spline

        int currentConnection = 0;

        for (int i = 0; i < connections.Length; i++)
        {
            if (connections[i].spline == tracer.spline && connections[i].pointIndex == passed[0].point)
            {
                currentConnection = i;

                break;
            }
        }

        //Choose a random connection to use that is not the current one
        //This part can be replaced with any other Junction-picking logic (see TrainEngine.cs in Examples)
        int newConnection = Random.Range(0, connections.Length);

        //If the random index corrensponds to the current connection, change it so that it points to another connection
        if (newConnection == currentConnection)
        {
            newConnection++;

            if (newConnection >= connections.Length) newConnection = 0;
        }

        //A good method to use which takes into account spline directions and travel distances
        //and adds compensation so that no twitching occurs

        Debug.Log($"Switching from connection {currentConnection} to {newConnection}");
        SwitchSpline(connections[currentConnection], connections[newConnection]);
    }

    void SwitchSpline(Node.Connection from, Node.Connection to)
    {
        //See how much units we have travelled past that Node in the last frame
        float excessDistance = tracer.spline.CalculateLength(tracer.spline.GetPointPercent(from.pointIndex), tracer.UnclipPercent(tracer.result.percent));

        //Set the spline to the tracer
        tracer.spline = to.spline;
        tracer.RebuildImmediate();

        //Get the location of the junction point in percent along the new spline
        double startpercent = tracer.ClipPercent(to.spline.GetPointPercent(to.pointIndex));

        if (Vector3.Dot(from.spline.Evaluate(from.pointIndex).forward, to.spline.Evaluate(to.pointIndex).forward) < 0f)
        {
            tracer.direction = tracer.direction == Spline.Direction.Forward ? Spline.Direction.Backward : Spline.Direction.Forward;
        }

        //Position the tracer at the new location and travel excessDistance along the new spline

        tracer.SetPercent(tracer.Travel(startpercent, excessDistance, tracer.direction));
    }
}