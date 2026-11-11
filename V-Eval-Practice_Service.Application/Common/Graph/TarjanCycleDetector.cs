namespace V_Eval_Practice_Service.Application.Common.Graph;

/// <summary>
/// Thuật toán Tarjan tìm các thành phần liên thông mạnh (SCC) để phát hiện chu trình kín
/// trong đồ thị tiên quyết kỹ năng (DAG Cycle Detection).
/// 
/// Đầu vào: Danh sách kề (adjacencyList) dạng skill_id -> danh sách prerequisite skill_id.
/// Đầu ra: Danh sách các tập đỉnh tạo thành vòng lặp phụ thuộc bất hợp lệ.
/// Nếu trả về rỗng => đồ thị hợp lệ (DAG thuần túy, không chu trình).
/// </summary>
public class TarjanCycleDetector
{
    private int _index;
    private readonly Stack<Guid> _stack = new();
    private readonly Dictionary<Guid, int> _dfn = new();
    private readonly Dictionary<Guid, int> _low = new();
    private readonly HashSet<Guid> _inStack = new();
    private readonly List<List<Guid>> _cycles = new();

    /// <summary>
    /// Phát hiện chu trình kín trong đồ thị kỹ năng tiên quyết.
    /// </summary>
    /// <param name="adjacencyList">Đồ thị dạng danh sách kề: skill_id -> danh sách các skill phụ thuộc (dependents)</param>
    /// <returns>Danh sách các tập đỉnh tạo thành vòng lặp kín. Rỗng nếu không có chu trình.</returns>
    public List<List<Guid>> DetectCycles(Dictionary<Guid, List<Guid>> adjacencyList)
    {
        _index = 0;
        _stack.Clear();
        _dfn.Clear();
        _low.Clear();
        _inStack.Clear();
        _cycles.Clear();

        foreach (var node in adjacencyList.Keys)
        {
            if (!_dfn.ContainsKey(node))
            {
                DFS(node, adjacencyList);
            }
        }

        return _cycles;
    }

    private void DFS(Guid u, Dictionary<Guid, List<Guid>> graph)
    {
        _dfn[u] = _low[u] = ++_index;
        _stack.Push(u);
        _inStack.Add(u);

        if (graph.TryGetValue(u, out var neighbors))
        {
            foreach (var v in neighbors)
            {
                if (!_dfn.ContainsKey(v))
                {
                    DFS(v, graph);
                    _low[u] = Math.Min(_low[u], _low[v]);
                }
                else if (_inStack.Contains(v))
                {
                    _low[u] = Math.Min(_low[u], _dfn[v]);
                }
            }
        }

        if (_dfn[u] == _low[u])
        {
            var scc = new List<Guid>();
            Guid w;
            do
            {
                w = _stack.Pop();
                _inStack.Remove(w);
                scc.Add(w);
            } while (w != u);

            // SCC có nhiều hơn 1 đỉnh => chu trình kín
            if (scc.Count > 1)
            {
                _cycles.Add(scc);
            }
            // Hoặc 1 đỉnh tự trỏ vào chính nó (self-loop)
            else if (scc.Count == 1 && graph.TryGetValue(u, out var selfNeighbors) && selfNeighbors.Contains(u))
            {
                _cycles.Add(scc);
            }
        }
    }
}
