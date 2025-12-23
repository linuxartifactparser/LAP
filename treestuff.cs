using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace LAP
{
    public partial class MainClass : Form
    {

        private string GetFolderDisplayName(string path)
        {
            string name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(name))
                name = path; // caso root

            try
            {
                int subdirs = Directory.GetDirectories(path).Length;
                int files = Directory.GetFiles(path).Length;
                return $"{name} ({files} files, {subdirs} folders)";
            }
            catch
            {
                return $"{name} (access denied)";
            }
        }


        private void LoadSubNodes(TreeNode node)
        {
            string path = node.Tag.ToString();

            try
            {
                // Aggiunge sottocartelle
                foreach (string dir in Directory.GetDirectories(path))
                {
                    TreeNode dirNode = new TreeNode(GetFolderDisplayName(dir))
                    {
                        Tag = dir,
                        ImageKey = "folder",
                        SelectedImageKey = "folder_open"
                    };

                    // Nodo "fittizio" per l’espansione successiva
                    dirNode.Nodes.Add(new TreeNode("Loading..."));
                    node.Nodes.Add(dirNode);
                }

                // Aggiunge file
                foreach (string file in Directory.GetFiles(path))
                {
                    TreeNode fileNode = new TreeNode(Path.GetFileName(file))
                    {
                        Tag = file,
                        ImageKey = "fileico",
                        SelectedImageKey = "fileico"
                    };
                    node.Nodes.Add(fileNode);
                }
            }
            catch (UnauthorizedAccessException)
            {
                node.Nodes.Add(new TreeNode("[Access denied]"));
            }
        }

        // --- EVENTO: prima di espandere ---
        private void treeView1_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            TreeNode node = e.Node;

            // Se è un nodo "fittizio", carica il contenuto reale
            if (node.Nodes.Count == 1 && node.Nodes[0].Text == "Loading...")
            {
                node.Nodes.Clear();
                LoadSubNodes(node);
            }

            node.ImageKey = "openfolderico";
            node.SelectedImageKey = "openfolderico";
        }

        // --- EVENTO: prima di chiudere ---
        private void treeView1_BeforeCollapse(object sender, TreeViewCancelEventArgs e)
        {
            TreeNode node = e.Node;
            node.ImageKey = "folderico";
            node.SelectedImageKey = "folderico";
        }

        // --- EVENTO: SELEZIONE DI UN NODO ---
        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node?.Tag == null)
                return;

            string path = e.Node.Tag.ToString();

            // Se è una CARTELLA
            if (Directory.Exists(path))
            {
                scintilla1.Text = string.Empty;
                hexBox2.ByteProvider = null;
                label8.Text = "MD5 : ";
                label7.Text = "SHA256 : ";
                linkLabel2.Text = string.Empty;
                return;
            }

            // Se è un FILE
            if (File.Exists(path))
            {
                try
                {
                    // 1️⃣ Mostra testo nel controllo Scintilla
                    string fileContent = File.ReadAllText(path);
                    scintilla1.Text = fileContent;

                    // 2️⃣ Mostra in hex viewer
                    var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                    hexBox2.ByteProvider = new Be.Windows.Forms.DynamicFileByteProvider(fs);
                    hexBox2.ReadOnly = true;

                    // 3️⃣ Calcola hash
                    string md5 = ComputeHash(path, "MD5");
                    string sha256 = ComputeHash(path, "SHA256");

                    // 4️⃣ Aggiorna labels
                    label8.Text = $"MD5 : {md5}";
                    label7.Text = $"SHA256 : {sha256}";

                    // 5️⃣ Nessun link
                    linkLabel2.Text = $"https://www.virustotal.com/gui/file/{sha256.ToUpper()}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error opening file:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // --- CALCOLA HASH ---
        private string ComputeHash(string filePath, string algorithm)
        {
            using (FileStream stream = File.OpenRead(filePath))
            {
                if (algorithm == "MD5")
                {
                    using (var md5 = MD5.Create())
                    {
                        byte[] hash = md5.ComputeHash(stream);
                        return BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
                    }
                }
                else if (algorithm == "SHA256")
                {
                    using (var sha = SHA256.Create())
                    {
                        byte[] hash = sha.ComputeHash(stream);
                        return BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
                    }
                }
                else
                {
                    throw new ArgumentException("Unsupported algorithm");
                }
            }
        }
    }
}
