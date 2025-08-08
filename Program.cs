using System.Net;
using System.Net.Sockets;
using System.Text;

/// <summary>
/// Serveur RAT (Remote Access Tool)
/// Ce serveur permet de gérer plusieurs connexions clients simultanées
/// et d'envoyer des commandes à distance aux clients connectés.
/// Protocol : TCP/IP sur le port 3001
/// Format des messages : UTF-8
/// </summary>
class Program
{
    /// <summary>
    /// Dictionnaire stockant les clients connectés avec leur ID
    /// </summary>
    static Dictionary<int, TcpClient> clients = new Dictionary<int, TcpClient>();

    /// <summary>
    /// ID qui sera attribué au prochain client
    /// </summary>
    static int nextClientId = 1;

    /// <summary>
    /// ID du client actuellement sélectionné (-1 pour tous les clients)
    /// </summary> 
    static int currentClientId = -1; // -1 signifie aucun client sélectionné

    /// <summary>
    /// Point d'entrée principal du serveur
    /// Cette méthode :
    /// 1. Initialise le serveur TCP sur le port 3001
    /// 2. Démarre l'écoute des connexions entrantes
    /// 3. Lance le gestionnaire de commandes en parallèle
    /// 4. Accepte et gère les nouvelles connexions clients
    /// </summary>
    /// <returns>Une tâche asynchrone qui ne se termine jamais (serveur toujours en écoute)</returns>
    static async Task Main()
    {
        var listener = new TcpListener(IPAddress.Any, 3001);
        listener.Start();
        Console.WriteLine("Serveur prêt sur le port 3001");

        // Démarrer le gestionnaire de commandes dans une tâche séparée
        _ = HandleServerCommands();

        while (true)
        {
            var client = await listener.AcceptTcpClientAsync();
            _ = HandleClient(client);
        }
    }

    /// <summary>
    /// Gère la connexion d'un nouveau client
    /// Cette méthode :
    /// 1. Attribue un ID unique au client
    /// 2. Ajoute le client au dictionnaire des clients actifs
    /// 3. Maintient une boucle de lecture des réponses du client
    /// 4. Gère la déconnexion propre du client
    /// 
    /// La méthode utilise un système de pooling avec délai pour éviter
    /// une utilisation excessive du CPU tout en maintenant une réactivité acceptable.
    /// </summary>
    /// <param name="client">Le TcpClient représentant la connexion avec le client</param>
    /// <returns>Une tâche asynchrone qui se termine à la déconnexion du client</returns>
    static async Task HandleClient(TcpClient client)
    {
        int clientId = nextClientId++;
        clients[clientId] = client;
        Console.WriteLine($"Client {clientId} connecté !");

        var stream = client.GetStream();
        try
        {
            var buffer = new byte[1024];
            while (true)
            {
                if (!clients.ContainsValue(client)) break; // Si le client a été retiré

                // Vérifier si des données sont disponibles
                if (client.Available > 0)
                {
                    int bytesRead = await stream.ReadAsync(buffer);
                    if (bytesRead == 0) break;
                    string reponse = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine($"\nRéponse du client {clientId} : {reponse}");
                }

                await Task.Delay(100); // Pour éviter une utilisation excessive du CPU
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nErreur client {clientId}: {ex.Message}");
        }
        finally
        {
            clients.Remove(clientId);
            client.Close();
            Console.WriteLine($"\nClient {clientId} déconnecté");
        }
    }

    /// <summary>
    /// Gère les commandes interactives de l'utilisateur
    /// Cette méthode implémente l'interface utilisateur interactive du serveur.
    /// 
    /// Fonctionnalités :
    /// - Affiche la liste des clients connectés
    /// - Permet de sélectionner un client spécifique ou tous les clients (-1)
    /// - Gère l'envoi des commandes aux clients :
    ///   * INFO : Demande les informations système
    ///   * ECHO|texte : Fait un écho du texte
    ///   * EXIT : Déconnecte le client
    /// 
    /// La méthode tourne en boucle continue et :
    /// 1. Vérifie la présence de clients
    /// 2. Affiche le statut actuel
    /// 3. Attend les commandes de l'utilisateur
    /// 4. Transmet les commandes aux clients sélectionnés
    /// </summary>
    /// <returns>Une tâche asynchrone qui ne se termine jamais (boucle continue)</returns>
    static async Task HandleServerCommands()
    {
        while (true)
        {
            if (clients.Count == 0)
            {
                Console.WriteLine("En attente de clients...");
                await Task.Delay(1000);
                continue;
            }

            if (currentClientId == -1)
            {
                Console.WriteLine("\nClients connectés :");
                foreach (var client in clients)
                {
                    Console.WriteLine($"Client ID: {client.Key}");
                }
                Console.WriteLine("\nCommandes spéciales :");
                Console.WriteLine("  !list - Afficher la liste des clients");
                Console.WriteLine("  !switch N - Changer vers le client N");
                Console.WriteLine("  !all - Sélectionner tous les clients");
            }
            else
            {
                Console.WriteLine($"\nClient actuel : {currentClientId} (utilisez !switch pour changer)");
            }

            Console.Write("\nCommande : ");
            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input)) continue;

            // Gestion des commandes spéciales
            if (input.StartsWith("!"))
            {
                var cmd = input.ToLower();
                if (cmd == "!list")
                {
                    foreach (var client in clients)
                    {
                        Console.WriteLine($"Client ID: {client.Key}");
                    }
                    continue;
                }
                else if (cmd.StartsWith("!switch "))
                {
                    if (int.TryParse(cmd.Split(' ')[1], out int newId) && clients.ContainsKey(newId))
                    {
                        currentClientId = newId;
                        Console.WriteLine($"Changement vers le client {newId}");
                    }
                    else
                    {
                        Console.WriteLine("ID de client invalide");
                    }
                    continue;
                }
                else if (cmd == "!all")
                {
                    currentClientId = -1;
                    Console.WriteLine("Mode diffusion à tous les clients");
                    continue;
                }
            }

            // Envoi de la commande
            string commande = input;
            if (string.IsNullOrWhiteSpace(commande)) continue;

            if (currentClientId == -1)
            {
                // Mode diffusion à tous les clients
                foreach (var client in clients.ToList())
                {
                    await SendCommand(client.Key, commande);
                }
            }
            else if (clients.ContainsKey(currentClientId))
            {
                await SendCommand(currentClientId, commande);
            }
            else
            {
                Console.WriteLine("ID de client invalide !");
            }
        }
    }

    /// <summary>
    /// Envoie une commande à un client spécifique
    /// </summary>
    static async Task SendCommand(int clientId, string commande)
    {
        if (!clients.TryGetValue(clientId, out var client))
        {
            Console.WriteLine($"Client {clientId} n'existe plus !");
            return;
        }

        try
        {
            var stream = client.GetStream();
            byte[] data = Encoding.UTF8.GetBytes(commande);
            await stream.WriteAsync(data);

            // Si c'est une commande EXIT, fermer le client
            if (commande == "EXIT")
            {
                clients.Remove(clientId);
                client.Close();
                Console.WriteLine($"Client {clientId} déconnecté !");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur avec le client {clientId}: {ex.Message}");
            clients.Remove(clientId);
            client.Close();
        }
    }
}
