﻿using UnityEngine;
using Unity.Collections;
using Unity.Networking.Transport;
using NetworkMessages;
using NetworkObjects;
using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

public class NetworkClient : MonoBehaviour
{
    public float updateTime = 1.0f / 30.0f;
    public NetworkDriver m_Driver;
    public NetworkConnection m_Connection;
    public string serverIP;
    public ushort serverPort;

    public string clientID;
    public List<NetworkObjects.NetworkPlayer> playerList = new List<NetworkObjects.NetworkPlayer>();
    public GameObject cubeRef;

    void Start ()
    {
        m_Driver = NetworkDriver.Create();
        m_Connection = default(NetworkConnection);
        var endpoint = NetworkEndPoint.Parse(serverIP,serverPort);
        m_Connection = m_Driver.Connect(endpoint);
    }
    
    void SendToServer(string message)
    {
        var writer = m_Driver.BeginSend(m_Connection);
        NativeArray<byte> bytes = new NativeArray<byte>(Encoding.ASCII.GetBytes(message),Allocator.Temp);
        writer.WriteBytes(bytes);
        m_Driver.EndSend(writer);
    }

    void OnConnect()
    {
        //PlayerUpdateMsg pm = new PlayerUpdateMsg();
        //pm.player.id = clientID;
        //SendToServer(JsonUtility.ToJson(pm));

        StartCoroutine(SendPlayerInformation());
    }

    IEnumerator SendPlayerInformation()
    {
        while (true)
        {
            PlayerUpdateMsg pum = new PlayerUpdateMsg();

            foreach (NetworkObjects.NetworkPlayer player in playerList)
            {
                if (player.id == clientID)
                {
                    pum.player.id = clientID;
                    pum.player.cube = player.cube;
                    pum.player.cubeColor = player.cubeColor;
                    pum.player.cubPos = player.cubPos;
                    pum.player.cubRot = player.cubRot;

                    SendToServer(JsonUtility.ToJson(pum));
                }
            }

            yield return new WaitForSeconds(updateTime);
        }
    }

    void OnData(DataStreamReader stream)
    {
        NativeArray<byte> bytes = new NativeArray<byte>(stream.Length,Allocator.Temp);
        stream.ReadBytes(bytes);
        string recMsg = Encoding.ASCII.GetString(bytes.ToArray());
        NetworkHeader header = JsonUtility.FromJson<NetworkHeader>(recMsg);

        Debug.Log("Got this: " + header.cmd);

        switch(header.cmd){
            case Commands.HANDSHAKE:
                HandshakeMsg hsMsg = JsonUtility.FromJson<HandshakeMsg>(recMsg);
                Debug.Log("Handshake message received!");
                clientID = hsMsg.player.id;
                break;

            case Commands.PLAYER_UPDATE:
                PlayerUpdateMsg puMsg = JsonUtility.FromJson<PlayerUpdateMsg>(recMsg);
                Debug.Log("Player update message received!");
                break;

            case Commands.PLAYER_JOINED:
                PlayerJoinMessage pjMsg = JsonUtility.FromJson<PlayerJoinMessage>(recMsg);
                Debug.Log("Player join message received!");
                pjMsg.player.cube = Instantiate(cubeRef);
                pjMsg.player.cube.GetComponent<Renderer>().material.SetColor("_Color", pjMsg.player.cubeColor);

                if(pjMsg.player.id == clientID)
                {
                    pjMsg.player.cube.AddComponent<PlayerController>();
                    pjMsg.player.cube.GetComponent<PlayerController>().playerRef = pjMsg.player;
                }

                playerList.Add(pjMsg.player);

                break;

            case Commands.SERVER_UPDATE:
                ServerUpdateMsg suMsg = JsonUtility.FromJson<ServerUpdateMsg>(recMsg);
                Debug.Log("Server update message received!");
                foreach(NetworkObjects.NetworkPlayer clientPlayer in playerList)
                {
                    if (clientPlayer.id == clientID)
                    {
                    }
                    else
                    {
                        foreach (NetworkObjects.NetworkPlayer serverPlayer in suMsg.players)
                        {
                            if (serverPlayer.id == clientPlayer.id)
                            {
                                clientPlayer.cube.transform.position = serverPlayer.cubPos;
                            }
                            else
                            {

                            }
                        }
                    }
                }
                break;
            case Commands.PLAYER_LEFT:
                PlayerLeaveMsg plMsg = JsonUtility.FromJson<PlayerLeaveMsg>(recMsg);
                Debug.Log("Player Leave message received!");
                KillPlayer(plMsg.player);
                OnDisconnect();

                break;
            default:
                Debug.Log("Unrecognized message received!");
                break;
        }
    }
    
    void KillPlayer(NetworkObjects.NetworkPlayer leavingPlayer)
    {
        foreach(NetworkObjects.NetworkPlayer player in playerList)
        {
            if(player.id == leavingPlayer.id)
            {
                Destroy(player.cube);
                playerList.Remove(player);
            }
        }
    }

    void Disconnect()
    {
        m_Connection.Disconnect(m_Driver);
        m_Connection = default(NetworkConnection);
    }

    void OnDisconnect()
    {
        Debug.Log("Client got disconnected from server");
        m_Connection = default(NetworkConnection);
    }

    public void OnDestroy()
    {
        m_Driver.Dispose();
    }   
    void Update()
    {
        m_Driver.ScheduleUpdate().Complete();

        if (!m_Connection.IsCreated)
        {
            return;
        }

        DataStreamReader stream;
        NetworkEvent.Type cmd;
        cmd = m_Connection.PopEvent(m_Driver, out stream);
        while (cmd != NetworkEvent.Type.Empty)
        {
            if (cmd == NetworkEvent.Type.Connect)
            {
                OnConnect();
            }
            else if (cmd == NetworkEvent.Type.Data)
            {
                OnData(stream);
            }
            else if (cmd == NetworkEvent.Type.Disconnect)
            {
                OnDisconnect();
            }

            cmd = m_Connection.PopEvent(m_Driver, out stream);
        }
    }
}