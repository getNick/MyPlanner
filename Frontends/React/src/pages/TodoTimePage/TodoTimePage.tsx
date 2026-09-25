import React, { useState } from 'react';
import { TaskItem } from './types';
import { DEFAULT_THEME } from './theme';
import {
  Clock,
  Play,
  Pause,
  CheckCircle2,
  Circle,
  Plus,
  Search,
  Tag,
  AlertTriangle,
  RotateCcw,
  Check,
  BarChart2,
  Filter,
  Trash2
} from 'lucide-react';

interface TodoTimePageProps {
  tasks?: TaskItem[];
  onToggleTask?: (id: string) => void;
  activeTheme?: typeof DEFAULT_THEME;
  isTimerRunning?: boolean;
  onToggleTimer?: () => void;
  secondsElapsed?: number;
}

export const TodoTimePage: React.FC<TodoTimePageProps> = ({
  tasks: initialTasks = [],
  onToggleTask,
  activeTheme = DEFAULT_THEME,
  isTimerRunning = false,
  onToggleTimer,
  secondsElapsed = 0,
}) => {
  const [taskList, setTaskList] = useState<TaskItem[]>(initialTasks);
  const [filterPriority, setFilterPriority] = useState<string>('all');
  const [filterProject, setFilterProject] = useState<string>('all');
  const [searchTerm, setSearchTerm] = useState('');

  // Form State
  const [newTitle, setNewTitle] = useState('');
  const [newProject, setNewProject] = useState('Engineering / Core');
  const [newPriority, setNewPriority] = useState<'urgent' | 'high' | 'medium' | 'low'>('high');
  const [newTag, setNewTag] = useState('Feature');
  const [newDueDate, setNewDueDate] = useState('Today at 6:00 PM');

  // Time sessions log
  const [timeLogs] = useState([
    { id: 'l1', taskTitle: 'Refactor Stripe Webhook Handlers', duration: '2h 15m', category: 'Backend', date: 'Today, 10:30 AM' },
    { id: 'l2', taskTitle: 'Review OCR Scan Results for AWS Cloud Invoice', duration: '45m', category: 'Finance', date: 'Today, 08:15 AM' },
    { id: 'l3', taskTitle: 'Q3 Budget Allocation Sync', duration: '1h 10m', category: 'Executive', date: 'Yesterday' },
  ]);

  const formatTime = (totalSec: number) => {
    const hrs = Math.floor(totalSec / 3600);
    const mins = Math.floor((totalSec % 3600) / 60);
    const secs = totalSec % 60;
    return `${hrs.toString().padStart(2, '0')}:${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
  };

  const handleAddTask = (e: React.FormEvent) => {
    e.preventDefault();
    if (!newTitle.trim()) return;

    const newTask: TaskItem = {
      id: `task-${Date.now()}`,
      title: newTitle.trim(),
      project: newProject,
      dueDate: newDueDate || 'Today',
      priority: newPriority,
      completed: false,
      tag: newTag || 'General',
    };

    setTaskList([newTask, ...taskList]);
    setNewTitle('');
  };

  const handleDeleteTask = (id: string) => {
    setTaskList(taskList.filter((t) => t.id !== id));
  };

  const handleToggle = (id: string) => {
    onToggleTask?.(id);
    setTaskList(
      taskList.map((t) => (t.id === id ? { ...t, completed: !t.completed } : t))
    );
  };

  // Filtering
  const filteredTasks = taskList.filter((t) => {
    const matchesSearch =
      t.title.toLowerCase().includes(searchTerm.toLowerCase()) ||
      t.project.toLowerCase().includes(searchTerm.toLowerCase()) ||
      t.tag.toLowerCase().includes(searchTerm.toLowerCase());
    const matchesPriority = filterPriority === 'all' || t.priority === filterPriority;
    const matchesProject = filterProject === 'all' || t.project.includes(filterProject);
    return matchesSearch && matchesPriority && matchesProject;
  });

  const completedCount = taskList.filter((t) => t.completed).length;
  const pendingCount = taskList.filter((t) => !t.completed).length;
  const completionPercentage = taskList.length > 0 ? Math.round((completedCount / taskList.length) * 100) : 0;

  return (
    <div className="space-y-6 font-mono">
      {/* HEADER SECTION */}
      <div className="flex flex-col md:flex-row md:items-center justify-between border-b border-slate-200 pb-4 gap-4">
        <div>
          <div className="flex items-center space-x-2 text-xs text-slate-500 uppercase tracking-widest">
            <Clock className="w-3.5 h-3.5 text-zinc-800" />
            <span>MODULE // TODO & TIME TRACKER</span>
          </div>
          <h1 className="text-2xl font-bold text-zinc-900 tracking-tight mt-0.5">
            TASK QUEUE & SESSION TIMER
          </h1>
        </div>

        {/* Top KPI Badges */}
        <div className="flex items-center space-x-2 text-xs">
          <div className="bg-white border border-slate-300 px-3 py-1.5 rounded-sm flex items-center space-x-2">
            <span className="text-slate-500">PENDING:</span>
            <span className="font-bold text-zinc-900">{pendingCount}</span>
          </div>
          <div className="bg-white border border-slate-300 px-3 py-1.5 rounded-sm flex items-center space-x-2">
            <span className="text-slate-500">COMPLETED:</span>
            <span className="font-bold text-emerald-700">{completedCount}</span>
          </div>
          <div className="bg-zinc-900 text-white px-3 py-1.5 rounded-sm font-bold">
            {completionPercentage}% DONE
          </div>
        </div>
      </div>

      {/* TOP SECTION: TIMER & NEW TASK FORM */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-5">
        {/* TIMER CONTROL BOX */}
        <div className="lg:col-span-5 bg-white border border-slate-300 p-5 rounded-sm flex flex-col justify-between">
          <div>
            <div className="flex items-center justify-between border-b border-slate-200 pb-3 mb-3">
              <span className="text-xs font-bold uppercase tracking-wider text-zinc-900 flex items-center space-x-2">
                <span className="w-2 h-2 bg-emerald-500 rounded-full animate-ping" />
                <span>PRECISION STOPWATCH</span>
              </span>
              <span className="text-[10px] bg-slate-100 text-slate-700 px-2 py-0.5 border border-slate-300 rounded-sm">
                TARGET: 8h 00m
              </span>
            </div>

            <div className="text-xs text-slate-600 mb-1">CURRENT ACTIVE TASK:</div>
            <div className="text-sm font-bold text-zinc-900 mb-4 bg-slate-50 p-2.5 border border-slate-200 rounded-sm">
              Refactoring Stripe Webhook Handlers & Idempotency
            </div>

            {/* Digital Timer Display */}
            <div className="bg-zinc-950 text-white p-4 rounded-sm border border-zinc-800 flex items-center justify-between">
              <div>
                <span className="text-[10px] text-zinc-400 block uppercase">SESSION ELAPSED</span>
                <span className="text-3xl font-bold tracking-wider">{formatTime(secondsElapsed)}</span>
              </div>
              <div className="flex items-center space-x-2">
                <button
                  onClick={onToggleTimer}
                  className="px-4 py-2.5 rounded-sm text-xs font-bold flex items-center space-x-1.5 transition-colors"
                  style={{ backgroundColor: activeTheme.swatchHex, color: '#ffffff' }}
                >
                  {isTimerRunning ? <Pause className="w-4 h-4" /> : <Play className="w-4 h-4" />}
                  <span>{isTimerRunning ? 'PAUSE' : 'START'}</span>
                </button>
                <button
                  onClick={() => alert('Timer reset')}
                  className="p-2.5 bg-zinc-800 hover:bg-zinc-700 text-zinc-300 rounded-sm"
                  title="Reset"
                >
                  <RotateCcw className="w-4 h-4" />
                </button>
              </div>
            </div>
          </div>

          <div className="mt-4 pt-3 border-t border-slate-200 flex justify-between text-xs text-slate-500">
            <span>SESSION TYPE: DEEP WORK</span>
            <span>AUTO-SYNC: ON</span>
          </div>
        </div>

        {/* CREATE TASK FORM */}
        <div className="lg:col-span-7 bg-white border border-slate-300 p-5 rounded-sm">
          <div className="border-b border-slate-200 pb-3 mb-4 flex items-center justify-between">
            <span className="text-xs font-bold uppercase tracking-wider text-zinc-900 flex items-center space-x-2">
              <Plus className="w-4 h-4 text-zinc-900" />
              <span>NEW TASK ENTRY</span>
            </span>
            <span className="text-[10px] text-slate-500">ENTER DETAILS BELOW</span>
          </div>

          <form onSubmit={handleAddTask} className="space-y-3">
            <div>
              <label className="block text-[11px] text-slate-600 uppercase mb-1">Task Description / Title *</label>
              <input
                type="text"
                value={newTitle}
                onChange={(e) => setNewTitle(e.target.value)}
                placeholder="e.g. Audit GCP Cloud Run memory limits & auto-scaling thresholds"
                className="w-full bg-slate-50 border border-slate-300 px-3 py-2 text-xs rounded-sm focus:outline-none focus:border-zinc-900 font-sans"
              />
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
              <div>
                <label className="block text-[11px] text-slate-600 uppercase mb-1">Project Category</label>
                <select
                  value={newProject}
                  onChange={(e) => setNewProject(e.target.value)}
                  className="w-full bg-slate-50 border border-slate-300 px-2 py-1.5 text-xs rounded-sm focus:outline-none focus:border-zinc-900"
                >
                  <option value="Engineering / Core">Engineering / Core</option>
                  <option value="Finance / Taxes">Finance / Taxes</option>
                  <option value="Product / Architecture">Product / Architecture</option>
                  <option value="Operations">Operations</option>
                </select>
              </div>

              <div>
                <label className="block text-[11px] text-slate-600 uppercase mb-1">Priority Level</label>
                <select
                  value={newPriority}
                  onChange={(e) => setNewPriority(e.target.value as any)}
                  className="w-full bg-slate-50 border border-slate-300 px-2 py-1.5 text-xs rounded-sm focus:outline-none focus:border-zinc-900 font-bold"
                >
                  <option value="urgent">URGENT</option>
                  <option value="high">HIGH</option>
                  <option value="medium">MEDIUM</option>
                  <option value="low">LOW</option>
                </select>
              </div>

              <div>
                <label className="block text-[11px] text-slate-600 uppercase mb-1">Tag Label</label>
                <input
                  type="text"
                  value={newTag}
                  onChange={(e) => setNewTag(e.target.value)}
                  placeholder="Backend"
                  className="w-full bg-slate-50 border border-slate-300 px-3 py-1.5 text-xs rounded-sm focus:outline-none focus:border-zinc-900"
                />
              </div>
            </div>

            <div className="flex items-center justify-between pt-2">
              <div className="text-[10px] text-slate-400">
                PRESSING 'CREATE TASK' ADDS ITEM TO QUEUE
              </div>
              <button
                type="submit"
                className="text-white px-5 py-2 rounded-sm text-xs font-bold transition-colors flex items-center space-x-1.5"
                style={{ backgroundColor: activeTheme.swatchHex }}
              >
                <Plus className="w-3.5 h-3.5" />
                <span>CREATE TASK</span>
              </button>
            </div>
          </form>
        </div>
      </div>

      {/* MAIN TASK TABLE / LIST */}
      <div className="bg-white border border-slate-300 p-5 rounded-sm">
        {/* Table Filters & Search */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between border-b border-slate-200 pb-4 mb-4 gap-3">
          <div className="flex items-center space-x-2">
            <Filter className="w-4 h-4 text-slate-500" />
            <span className="text-xs font-bold text-zinc-900 uppercase">TASK QUEUE ({filteredTasks.length})</span>
          </div>

          <div className="flex flex-wrap items-center gap-2">
            {/* Search */}
            <div className="relative">
              <Search className="w-3.5 h-3.5 text-slate-400 absolute left-2.5 top-2" />
              <input
                type="text"
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                placeholder="Search tasks..."
                className="bg-slate-50 border border-slate-300 pl-8 pr-3 py-1 text-xs rounded-sm focus:outline-none focus:border-zinc-900 w-44"
              />
            </div>

            {/* Priority Filter */}
            <select
              value={filterPriority}
              onChange={(e) => setFilterPriority(e.target.value)}
              className="bg-slate-50 border border-slate-300 px-2 py-1 text-xs rounded-sm focus:outline-none focus:border-zinc-900"
            >
              <option value="all">Priority: All</option>
              <option value="urgent">Priority: Urgent</option>
              <option value="high">Priority: High</option>
              <option value="medium">Priority: Medium</option>
              <option value="low">Priority: Low</option>
            </select>

            {/* Project Filter */}
            <select
              value={filterProject}
              onChange={(e) => setFilterProject(e.target.value)}
              className="bg-slate-50 border border-slate-300 px-2 py-1 text-xs rounded-sm focus:outline-none focus:border-zinc-900"
            >
              <option value="all">Project: All</option>
              <option value="Engineering">Engineering</option>
              <option value="Finance">Finance</option>
              <option value="Personal">Personal</option>
              <option value="Strategic">Strategic</option>
            </select>
          </div>
        </div>

        {/* Task Items Render */}
        {filteredTasks.length === 0 ? (
          <div className="text-center py-10 border border-dashed border-slate-300 rounded-sm text-slate-500 text-xs">
            NO TASKS MATCH THE CURRENT FILTER
          </div>
        ) : (
          <div className="space-y-2">
            {filteredTasks.map((task) => (
              <div
                key={task.id}
                className={`p-3.5 border rounded-sm transition-all flex flex-col sm:flex-row sm:items-center justify-between gap-3 ${
                  task.completed
                    ? 'bg-slate-50 border-slate-200 opacity-60'
                    : 'bg-white border-slate-300 hover:border-zinc-500'
                }`}
              >
                <div className="flex items-start space-x-3 flex-1 min-w-0">
                  <button
                    onClick={() => handleToggle(task.id)}
                    className="mt-0.5 text-slate-700 hover:text-zinc-950 transition-colors"
                  >
                    {task.completed ? (
                      <CheckCircle2
                        className="w-4 h-4 stroke-[2.5]"
                        style={{ color: activeTheme.swatchHex }}
                      />
                    ) : (
                      <Circle className="w-4 h-4 text-slate-400 stroke-[2]" />
                    )}
                  </button>

                  <div className="flex-1 min-w-0">
                    <div
                      className={`text-xs font-bold leading-tight ${
                        task.completed ? 'line-through text-slate-400' : 'text-zinc-900'
                      }`}
                    >
                      {task.title}
                    </div>
                    <div className="flex items-center space-x-2 text-[10px] text-slate-500 mt-1">
                      <span className="font-semibold text-slate-700">{task.project}</span>
                      <span>•</span>
                      <span>DUE: {task.dueDate}</span>
                    </div>
                  </div>
                </div>

                <div className="flex items-center space-x-2 shrink-0 self-end sm:self-center">
                  <span className="text-[10px] bg-slate-100 text-slate-700 px-2 py-0.5 border border-slate-300 rounded-sm">
                    #{task.tag}
                  </span>

                  <span
                    className={`text-[10px] font-bold px-2 py-0.5 rounded-sm uppercase ${
                      task.priority === 'urgent'
                        ? 'bg-rose-100 text-rose-800 border border-rose-300'
                        : task.priority === 'high'
                        ? 'bg-amber-100 text-amber-800 border border-amber-300'
                        : 'bg-slate-100 text-slate-700 border border-slate-300'
                    }`}
                  >
                    [{task.priority}]
                  </span>

                  <button
                    onClick={() => handleDeleteTask(task.id)}
                    className="p-1 text-slate-400 hover:text-rose-600 transition-colors"
                    title="Delete task"
                  >
                    <Trash2 className="w-3.5 h-3.5" />
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* SESSION LOGS HISTORY TABLE */}
      <div className="bg-white border border-slate-300 p-5 rounded-sm">
        <div className="flex items-center justify-between border-b border-slate-200 pb-3 mb-3">
          <span className="text-xs font-bold uppercase tracking-wider text-zinc-900 flex items-center space-x-2">
            <BarChart2 className="w-4 h-4 text-slate-700" />
            <span>RECENT TIME TRACKING SESSIONS</span>
          </span>
          <span className="text-[10px] text-slate-500">TOTAL TODAY: 7h 12m</span>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead>
              <tr className="border-b border-slate-200 text-[10px] uppercase text-slate-500">
                <th className="py-2 px-3">Session Task Name</th>
                <th className="py-2 px-3">Category</th>
                <th className="py-2 px-3">Duration</th>
                <th className="py-2 px-3 text-right">Timestamp</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {timeLogs.map((log) => (
                <tr key={log.id} className="hover:bg-slate-50">
                  <td className="py-2.5 px-3 font-bold text-zinc-900">{log.taskTitle}</td>
                  <td className="py-2.5 px-3">
                    <span className="bg-slate-100 border border-slate-300 text-slate-700 px-2 py-0.5 text-[10px] rounded-sm">
                      {log.category}
                    </span>
                  </td>
                  <td className="py-2.5 px-3 font-bold text-emerald-700">{log.duration}</td>
                  <td className="py-2.5 px-3 text-right text-slate-500 text-[11px]">{log.date}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};

export default TodoTimePage;
