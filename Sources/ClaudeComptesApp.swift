import AppKit
import SwiftUI
import Foundation

@main
struct ClaudeComptesApp: App {
    @StateObject private var store = ProfileStore()

    var body: some Scene {
        WindowGroup {
            ContentView()
                .environmentObject(store)
                .frame(minWidth: 720, minHeight: 560)
        }
        .windowStyle(.hiddenTitleBar)
        .commands {
            CommandGroup(replacing: .newItem) { }
        }
    }
}

struct ClaudeProfile: Codable, Identifiable, Equatable {
    var id: UUID
    var name: String
    var color: String
    var createdAt: Date

    var dataDirectory: URL {
        FileManager.default.homeDirectoryForCurrentUser
            .appendingPathComponent("Library/Application Support/Claude Multi Comptes/Profiles", isDirectory: true)
            .appendingPathComponent(id.uuidString, isDirectory: true)
    }

    static let colors = ["#D97757", "#527A69", "#5574A6", "#9470A8", "#C18443", "#68737D"]
}

@MainActor
final class ProfileStore: ObservableObject {
    static let supportDirectory = FileManager.default.homeDirectoryForCurrentUser
        .appendingPathComponent("Library/Application Support/Claude Multi Comptes", isDirectory: true)
    static let profilesDirectory = supportDirectory.appendingPathComponent("Profiles", isDirectory: true)

    @Published private(set) var profiles: [ClaudeProfile] = []
    @Published private(set) var runningProfileIDs: Set<UUID> = []
    @Published var alertMessage: String?
    @Published private(set) var isUpdatingClaudeCode = false
    @Published private(set) var currentClaudeCodeVersion: String?
    @Published private(set) var latestClaudeCodeVersion: String?
    @Published private(set) var isCheckingClaudeCodeVersion = false
    @Published private(set) var versionCheckError: String?

    private var refreshTimer: Timer?
    private let claudeExecutable = URL(fileURLWithPath: "/Applications/Claude.app/Contents/MacOS/Claude")

    init() {
        loadProfiles()
        refreshRunningProfiles()
        checkClaudeCodeVersions()
        refreshTimer = Timer.scheduledTimer(withTimeInterval: 2, repeats: true) { [weak self] _ in
            Task { @MainActor in self?.refreshRunningProfiles() }
        }
    }

    var activeCount: Int { runningProfileIDs.count }
    var claudeIsInstalled: Bool { FileManager.default.isExecutableFile(atPath: claudeExecutable.path) }
    var claudeCodeExecutable: URL? {
        let home = FileManager.default.homeDirectoryForCurrentUser
        let candidates = [
            home.appendingPathComponent(".local/bin/claude"),
            URL(fileURLWithPath: "/opt/homebrew/bin/claude"),
            URL(fileURLWithPath: "/usr/local/bin/claude")
        ]
        return candidates.first { FileManager.default.isExecutableFile(atPath: $0.path) }
    }

    func openClaudeDownloadPage() {
        guard let url = URL(string: "https://claude.com/download"), NSWorkspace.shared.open(url) else {
            alertMessage = "Impossible d’ouvrir la page officielle de téléchargement de Claude."
            return
        }
    }

    func checkClaudeCodeVersions() {
        guard !isCheckingClaudeCodeVersion else { return }
        guard let executable = claudeCodeExecutable else {
            currentClaudeCodeVersion = nil
            latestClaudeCodeVersion = nil
            versionCheckError = "Claude Code introuvable"
            return
        }

        isCheckingClaudeCodeVersion = true
        versionCheckError = nil
        Task {
            async let current = Self.readCurrentVersion(from: executable)
            async let latest = Self.fetchLatestVersion()
            do {
                let (installedVersion, latestVersion) = try await (current, latest)
                currentClaudeCodeVersion = installedVersion
                latestClaudeCodeVersion = latestVersion
                if installedVersion == nil { versionCheckError = "Version installée illisible" }
            } catch {
                versionCheckError = "Impossible de vérifier la dernière version"
            }
            isCheckingClaudeCodeVersion = false
        }
    }

    func updateClaudeCode() {
        guard !isUpdatingClaudeCode else { return }
        guard let executable = claudeCodeExecutable else {
            alertMessage = "Claude Code est introuvable. Installe-le d’abord depuis la documentation officielle."
            return
        }

        let process = Process()
        let outputPipe = Pipe()
        process.executableURL = executable
        process.arguments = ["update"]
        process.standardOutput = outputPipe
        process.standardError = outputPipe
        var environment = ProcessInfo.processInfo.environment
        let extraPaths = [executable.deletingLastPathComponent().path, "/opt/homebrew/bin", "/usr/local/bin", "/usr/bin", "/bin"]
        environment["PATH"] = (extraPaths + [environment["PATH"] ?? ""]).joined(separator: ":")
        process.environment = environment

        do {
            try process.run()
            isUpdatingClaudeCode = true
            DispatchQueue.global(qos: .userInitiated).async { [weak self] in
                let outputData = outputPipe.fileHandleForReading.readDataToEndOfFile()
                process.waitUntilExit()
                let output = String(decoding: outputData, as: UTF8.self)
                    .trimmingCharacters(in: .whitespacesAndNewlines)
                let succeeded = process.terminationStatus == 0
                Task { @MainActor in
                    guard let self else { return }
                    self.isUpdatingClaudeCode = false
                    if succeeded {
                        self.alertMessage = output.isEmpty
                            ? "Claude Code est à jour."
                            : "Mise à jour Claude Code terminée.\n\n\(output.suffix(1200))"
                        self.checkClaudeCodeVersions()
                    } else {
                        self.alertMessage = "La mise à jour Claude Code a échoué (code \(process.terminationStatus)).\n\n\(output.suffix(1200))"
                    }
                }
            }
        } catch {
            isUpdatingClaudeCode = false
            alertMessage = "Impossible de démarrer la mise à jour de Claude Code : \(error.localizedDescription)"
        }
    }

    private static func readCurrentVersion(from executable: URL) async -> String? {
        await withCheckedContinuation { continuation in
            DispatchQueue.global(qos: .utility).async {
                let process = Process()
                let pipe = Pipe()
                process.executableURL = executable
                process.arguments = ["--version"]
                process.standardOutput = pipe
                process.standardError = Pipe()
                do {
                    try process.run()
                    let data = pipe.fileHandleForReading.readDataToEndOfFile()
                    process.waitUntilExit()
                    guard process.terminationStatus == 0 else {
                        continuation.resume(returning: nil)
                        return
                    }
                    let version = String(decoding: data, as: UTF8.self)
                        .trimmingCharacters(in: .whitespacesAndNewlines)
                        .split(whereSeparator: \.isWhitespace)
                        .first.map(String.init)
                        .map { $0.trimmingCharacters(in: CharacterSet(charactersIn: "v")) }
                    continuation.resume(returning: version)
                } catch {
                    continuation.resume(returning: nil)
                }
            }
        }
    }

    private static func fetchLatestVersion() async throws -> String {
        let url = URL(string: "https://api.github.com/repos/anthropics/claude-code/releases/latest")!
        var request = URLRequest(url: url)
        request.setValue("application/vnd.github+json", forHTTPHeaderField: "Accept")
        request.setValue("Claude Multi Comptes macOS", forHTTPHeaderField: "User-Agent")
        let (data, response) = try await URLSession.shared.data(for: request)
        guard let httpResponse = response as? HTTPURLResponse,
              (200..<300).contains(httpResponse.statusCode) else {
            throw URLError(.badServerResponse)
        }
        let release = try JSONDecoder().decode(GitHubRelease.self, from: data)
        return release.tagName.trimmingCharacters(in: CharacterSet(charactersIn: "v"))
    }

    private struct GitHubRelease: Decodable {
        var tagName: String
        enum CodingKeys: String, CodingKey { case tagName = "tag_name" }
    }

    private func isNewer(_ candidate: String, than installed: String) -> Bool {
        let candidateParts = candidate.split(separator: ".").compactMap { Int($0) }
        let installedParts = installed.split(separator: ".").compactMap { Int($0) }
        guard !candidateParts.isEmpty, !installedParts.isEmpty else { return false }
        for index in 0..<max(candidateParts.count, installedParts.count) {
            let candidatePart = index < candidateParts.count ? candidateParts[index] : 0
            let installedPart = index < installedParts.count ? installedParts[index] : 0
            if candidatePart != installedPart { return candidatePart > installedPart }
        }
        return false
    }

    var claudeCodeUpdateAvailable: Bool? {
        guard let latestClaudeCodeVersion, let currentClaudeCodeVersion else { return nil }
        return isNewer(latestClaudeCodeVersion, than: currentClaudeCodeVersion)
    }

    func addProfile(name: String, color: String) {
        let cleanName = name.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !cleanName.isEmpty else { return }
        let profile = ClaudeProfile(id: UUID(), name: cleanName, color: color, createdAt: .now)
        profiles.append(profile)
        saveProfiles()
    }

    func rename(_ profile: ClaudeProfile, to name: String) {
        guard let index = profiles.firstIndex(where: { $0.id == profile.id }) else { return }
        let cleanName = name.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !cleanName.isEmpty else { return }
        profiles[index].name = cleanName
        saveProfiles()
    }

    func remove(_ profile: ClaudeProfile) {
        profiles.removeAll { $0.id == profile.id }
        saveProfiles()
        // Keep the account data directory so removing a card cannot erase a login or conversations.
    }

    func launch(_ profile: ClaudeProfile) {
        refreshRunningProfiles()
        guard claudeIsInstalled else {
            alertMessage = "Claude Desktop est introuvable dans le dossier Applications. Installe Claude, puis réessaie."
            return
        }
        if runningProfileIDs.contains(profile.id) {
            alertMessage = "Ce compte est déjà ouvert. Ferme sa fenêtre Claude avant de le relancer."
            return
        }
        do {
            try FileManager.default.createDirectory(at: profile.dataDirectory, withIntermediateDirectories: true)
            let process = Process()
            process.executableURL = claudeExecutable
            process.arguments = ["--user-data-dir=\(profile.dataDirectory.path)"]
            try process.run()
            // Process exits when Claude's main process exits. Polling ps also detects launches
            // that remain open after this manager is quit and reopened.
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.8) { [weak self] in
                self?.refreshRunningProfiles()
            }
        } catch {
            alertMessage = "Impossible de lancer Claude : \(error.localizedDescription)"
        }
    }

    func refreshRunningProfiles() {
        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/bin/ps")
        process.arguments = ["-axo", "command="]
        let pipe = Pipe()
        process.standardOutput = pipe
        process.standardError = Pipe()
        do {
            try process.run()
            let output = pipe.fileHandleForReading.readDataToEndOfFile()
            process.waitUntilExit()
            let commands = String(decoding: output, as: UTF8.self)
            let activeIDs = Set(profiles.compactMap { profile -> UUID? in
                let marker = "--user-data-dir=\(profile.dataDirectory.path)"
                return commands.contains(marker) ? profile.id : nil
            })
            if activeIDs != runningProfileIDs { runningProfileIDs = activeIDs }
        } catch {
            // The UI remains usable if process inspection is unavailable.
        }
    }

    private var indexURL: URL { Self.supportDirectory.appendingPathComponent("profiles.json") }

    private func loadProfiles() {
        do {
            try FileManager.default.createDirectory(at: Self.profilesDirectory, withIntermediateDirectories: true)
            let data = try Data(contentsOf: indexURL)
            profiles = try JSONDecoder().decode([ClaudeProfile].self, from: data)
        } catch {
            profiles = []
        }
    }

    private func saveProfiles() {
        do {
            try FileManager.default.createDirectory(at: Self.supportDirectory, withIntermediateDirectories: true)
            let encoder = JSONEncoder()
            encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
            try encoder.encode(profiles).write(to: indexURL, options: .atomic)
        } catch {
            alertMessage = "Impossible d’enregistrer les comptes : \(error.localizedDescription)"
        }
    }
}

struct ContentView: View {
    @EnvironmentObject private var store: ProfileStore
    @State private var isAddingProfile = false
    @State private var profileToRename: ClaudeProfile?
    @State private var profileToRemove: ClaudeProfile?

    var body: some View {
        ZStack {
            Color(nsColor: .windowBackgroundColor)
                .ignoresSafeArea()
            VStack(spacing: 0) {
                header
                ScrollView {
                    VStack(alignment: .leading, spacing: 24) {
                        introCard
                        profilesSection
                        updatesSection
                        footerNote
                    }
                    .padding(.horizontal, 32)
                    .padding(.vertical, 28)
                }
            }
        }
        .alert("Claude Multi Comptes", isPresented: Binding(
            get: { store.alertMessage != nil },
            set: { if !$0 { store.alertMessage = nil } }
        )) {
            Button("OK", role: .cancel) { store.alertMessage = nil }
        } message: {
            Text(store.alertMessage ?? "")
        }
        .sheet(isPresented: $isAddingProfile) { AddProfileSheet { name, color in store.addProfile(name: name, color: color) } }
        .sheet(item: $profileToRename) { profile in
            RenameProfileSheet(name: profile.name) { store.rename(profile, to: $0) }
        }
        .confirmationDialog("Retirer ce compte de la liste ?", isPresented: Binding(
            get: { profileToRemove != nil },
            set: { if !$0 { profileToRemove = nil } }
        ), titleVisibility: .visible) {
            Button("Retirer de la liste", role: .destructive) {
                if let profileToRemove { store.remove(profileToRemove) }
                profileToRemove = nil
            }
            Button("Annuler", role: .cancel) { profileToRemove = nil }
        } message: {
            Text("Les données du compte et sa session restent sur ce Mac.")
        }
    }

    private var header: some View {
        HStack(spacing: 13) {
            ZStack {
                RoundedRectangle(cornerRadius: 11).fill(Color(red: 0.82, green: 0.40, blue: 0.29))
                Image(systemName: "person.2.fill").font(.system(size: 16, weight: .semibold)).foregroundStyle(.white)
            }
            .frame(width: 36, height: 36)
            VStack(alignment: .leading, spacing: 2) {
                Text("Claude Multi Comptes").font(.system(size: 14, weight: .semibold))
                Text("Tes espaces, chacun à sa place").font(.system(size: 11)).foregroundStyle(.secondary)
            }
            Spacer()
            HStack(spacing: 7) {
                Circle().fill(store.claudeIsInstalled ? Color.green : Color.orange).frame(width: 7, height: 7)
                Text(store.claudeIsInstalled ? "Claude prêt" : "Claude absent")
                    .font(.system(size: 11, weight: .medium)).foregroundStyle(.secondary)
            }
            .padding(.horizontal, 11).padding(.vertical, 7)
            .background(.regularMaterial, in: Capsule())
        }
        .padding(.horizontal, 28).padding(.vertical, 17)
        .background(.ultraThinMaterial)
        .overlay(alignment: .bottom) { Rectangle().fill(Color.primary.opacity(0.06)).frame(height: 1) }
    }

    private var introCard: some View {
        HStack(alignment: .center, spacing: 20) {
            VStack(alignment: .leading, spacing: 11) {
                Text("Tous tes comptes Claude.").font(.system(size: 25, weight: .semibold, design: .rounded))
                Text("Crée un espace par compte, connecte-toi une fois, puis lance les sessions dont tu as besoin.")
                    .font(.system(size: 13)).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
                    .frame(maxWidth: 440, alignment: .leading)
                HStack(spacing: 7) {
                    Image(systemName: "lock.shield.fill").foregroundStyle(Color(red: 0.28, green: 0.52, blue: 0.39))
                    Text("Sessions gardées séparément sur ce Mac").foregroundStyle(.secondary)
                }
                .font(.system(size: 11, weight: .medium))
            }
            Spacer(minLength: 8)
            Button { isAddingProfile = true } label: {
                Label("Ajouter un compte", systemImage: "plus")
                    .font(.system(size: 12, weight: .semibold))
                    .padding(.horizontal, 14).padding(.vertical, 10)
            }
            .buttonStyle(.borderedProminent)
            .tint(Color(red: 0.78, green: 0.37, blue: 0.27))
            .disabled(!store.claudeIsInstalled)
        }
        .padding(23)
        .background(Color(nsColor: .controlBackgroundColor), in: RoundedRectangle(cornerRadius: 17))
        .overlay(RoundedRectangle(cornerRadius: 17).stroke(Color.primary.opacity(0.06), lineWidth: 1))
    }

    private var profilesSection: some View {
        VStack(alignment: .leading, spacing: 13) {
            HStack(alignment: .firstTextBaseline) {
                Text("MES COMPTES").font(.system(size: 10, weight: .bold)).tracking(1.1).foregroundStyle(.secondary)
                Spacer()
                Text("\(store.activeCount) ouverts").font(.system(size: 11, weight: .medium, design: .monospaced)).foregroundStyle(.secondary)
            }
            if store.profiles.isEmpty {
                emptyState
            } else {
                VStack(spacing: 9) {
                    ForEach(store.profiles) { profile in profileRow(profile) }
                }
            }
        }
    }

    private var updatesSection: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack(spacing: 8) {
                Image(systemName: "arrow.triangle.2.circlepath")
                    .font(.system(size: 10, weight: .bold))
                    .foregroundStyle(Color(red: 0.78, green: 0.37, blue: 0.27))
                Text("MISES À JOUR")
                    .font(.system(size: 10, weight: .bold))
                    .tracking(1.1)
                    .foregroundStyle(.secondary)
                Spacer()
                Text("Claude Desktop · Claude Code")
                    .font(.system(size: 10))
                    .foregroundStyle(.tertiary)
            }
            HStack(spacing: 12) {
                updateButton(
                    title: "Mettre à jour Claude Desktop",
                    detail: "Automatique dans l’app · téléchargement manuel",
                    icon: "arrow.down.app",
                    isBusy: false,
                    isEnabled: true,
                    action: store.openClaudeDownloadPage
                )
                updateButton(
                    title: "Mettre à jour Claude Code",
                    detail: store.isUpdatingClaudeCode ? "Mise à jour en cours…" : "Lancer la commande officielle",
                    icon: store.isUpdatingClaudeCode ? "arrow.triangle.2.circlepath" : "chevron.left.forwardslash.chevron.right",
                    isBusy: store.isUpdatingClaudeCode,
                    isEnabled: store.claudeCodeExecutable != nil && !store.isUpdatingClaudeCode,
                    action: store.updateClaudeCode
                )
            }
            claudeCodeVersionRow
        }
        .padding(16)
        .background(Color(nsColor: .controlBackgroundColor).opacity(0.72), in: RoundedRectangle(cornerRadius: 16))
        .overlay(RoundedRectangle(cornerRadius: 16).stroke(Color.primary.opacity(0.08), lineWidth: 1))
    }

    private var claudeCodeVersionRow: some View {
        HStack(spacing: 9) {
            Image(systemName: "chevron.left.forwardslash.chevron.right")
                .font(.system(size: 10, weight: .semibold)).foregroundStyle(.secondary)
            Text("Claude Code installée").font(.system(size: 10)).foregroundStyle(.secondary)
            Text(store.currentClaudeCodeVersion ?? "—")
                .font(.system(size: 10, weight: .semibold, design: .monospaced))
            Text("·").foregroundStyle(.tertiary)
            Text("dernière publiée").font(.system(size: 10)).foregroundStyle(.secondary)
            Text(store.latestClaudeCodeVersion ?? (store.isCheckingClaudeCodeVersion ? "Vérification…" : "—"))
                .font(.system(size: 10, weight: .semibold, design: .monospaced))
            Spacer(minLength: 2)
            if let updateAvailable = store.claudeCodeUpdateAvailable {
                Text(updateAvailable ? "Claude Code à mettre à jour" : "Claude Code à jour")
                    .font(.system(size: 10, weight: .semibold))
                    .foregroundStyle(updateAvailable ? Color.orange : Color.green)
            } else if let versionCheckError = store.versionCheckError {
                Text(versionCheckError).font(.system(size: 9)).foregroundStyle(.secondary).lineLimit(1)
            }
            Button { store.checkClaudeCodeVersions() } label: {
                if store.isCheckingClaudeCodeVersion {
                    ProgressView().controlSize(.mini).frame(width: 18, height: 18)
                } else {
                    Image(systemName: "arrow.clockwise").font(.system(size: 10, weight: .semibold)).frame(width: 18, height: 18)
                }
            }
            .buttonStyle(.plain)
            .disabled(store.isCheckingClaudeCodeVersion || store.claudeCodeExecutable == nil)
            .help("Vérifier les versions")
        }
        .padding(.horizontal, 12).padding(.vertical, 9)
        .background(Color(nsColor: .controlBackgroundColor).opacity(0.65), in: RoundedRectangle(cornerRadius: 11))
    }

    private func updateButton(
        title: String,
        detail: String,
        icon: String,
        isBusy: Bool,
        isEnabled: Bool,
        action: @escaping () -> Void
    ) -> some View {
        Button(action: action) {
            HStack(spacing: 12) {
                ZStack {
                    RoundedRectangle(cornerRadius: 10)
                        .fill(Color(red: 0.78, green: 0.37, blue: 0.27).opacity(0.11))
                    if isBusy {
                        ProgressView().controlSize(.small)
                    } else {
                        Image(systemName: icon).font(.system(size: 14, weight: .semibold))
                            .foregroundStyle(Color(red: 0.78, green: 0.37, blue: 0.27))
                    }
                }
                .frame(width: 36, height: 36)
                VStack(alignment: .leading, spacing: 3) {
                    Text(title).font(.system(size: 12, weight: .semibold)).foregroundStyle(.primary)
                    Text(detail).font(.system(size: 10)).foregroundStyle(.secondary)
                }
                Spacer(minLength: 3)
                Image(systemName: "arrow.up.right").font(.system(size: 10, weight: .semibold)).foregroundStyle(.tertiary)
            }
            .padding(12)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(Color(nsColor: .controlBackgroundColor), in: RoundedRectangle(cornerRadius: 14))
            .overlay(RoundedRectangle(cornerRadius: 14).stroke(Color.primary.opacity(0.055), lineWidth: 1))
            .contentShape(RoundedRectangle(cornerRadius: 14))
        }
        .buttonStyle(.plain)
        .disabled(!isEnabled)
        .opacity(isEnabled ? 1 : 0.55)
    }

    private var emptyState: some View {
        VStack(spacing: 10) {
            Image(systemName: "person.crop.circle.badge.plus").font(.system(size: 26, weight: .light)).foregroundStyle(.secondary)
            Text("Aucun compte pour le moment").font(.system(size: 14, weight: .semibold))
            Text("Ajoute ton compte personnel, professionnel ou un espace client.")
                .font(.system(size: 12)).foregroundStyle(.secondary)
            Button("Créer mon premier compte") { isAddingProfile = true }
                .buttonStyle(.link).font(.system(size: 12, weight: .medium))
                .disabled(!store.claudeIsInstalled)
        }
        .frame(maxWidth: .infinity).padding(.vertical, 29)
        .background(Color(nsColor: .controlBackgroundColor), in: RoundedRectangle(cornerRadius: 15))
        .overlay(RoundedRectangle(cornerRadius: 15).strokeBorder(Color.primary.opacity(0.06), style: StrokeStyle(lineWidth: 1, dash: [5, 4])))
    }

    private func profileRow(_ profile: ClaudeProfile) -> some View {
        let isRunning = store.runningProfileIDs.contains(profile.id)
        return HStack(spacing: 14) {
            ZStack {
                RoundedRectangle(cornerRadius: 12).fill(Color(hex: profile.color).opacity(0.14))
                Image(systemName: "person.fill").font(.system(size: 15, weight: .medium)).foregroundStyle(Color(hex: profile.color))
            }
            .frame(width: 42, height: 42)
            VStack(alignment: .leading, spacing: 4) {
                Text(profile.name).font(.system(size: 13, weight: .semibold))
                HStack(spacing: 6) {
                    Circle().fill(isRunning ? Color.green : Color.secondary.opacity(0.45)).frame(width: 6, height: 6)
                    Text(isRunning ? "Ouvert" : "Prêt à ouvrir").font(.system(size: 11)).foregroundStyle(.secondary)
                }
            }
            Spacer()
            Menu {
                Button("Renommer…") { profileToRename = profile }
                Button("Retirer de la liste…", role: .destructive) { profileToRemove = profile }
                    .disabled(isRunning)
            } label: {
                Image(systemName: "ellipsis").font(.system(size: 14, weight: .semibold)).foregroundStyle(.secondary)
                    .frame(width: 30, height: 30).contentShape(Rectangle())
            }
            .menuStyle(.borderlessButton)
            Button { store.launch(profile) } label: {
                Label(isRunning ? "Déjà ouvert" : "Ouvrir", systemImage: isRunning ? "checkmark" : "arrow.up.right")
                    .font(.system(size: 11, weight: .semibold)).frame(minWidth: 76)
            }
            .buttonStyle(.bordered)
            .disabled(isRunning || !store.claudeIsInstalled)
        }
        .padding(.horizontal, 15).padding(.vertical, 12)
        .background(Color(nsColor: .controlBackgroundColor), in: RoundedRectangle(cornerRadius: 14))
        .overlay(RoundedRectangle(cornerRadius: 14).stroke(Color.primary.opacity(0.055), lineWidth: 1))
    }

    private var footerNote: some View {
        HStack(alignment: .top, spacing: 8) {
            Image(systemName: "info.circle").foregroundStyle(.secondary)
            Text("Au premier lancement, connecte-toi au compte choisi dans la fenêtre Claude. Pour libérer une place, ferme cette fenêtre Claude. Les réglages et connexions propres à chaque profil restent séparés.")
                .font(.system(size: 11)).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
        }
        .padding(.top, 2)
    }
}

struct AddProfileSheet: View {
    @Environment(\.dismiss) private var dismiss
    @State private var name = ""
    @State private var color = ClaudeProfile.colors[0]
    let onCreate: (String, String) -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 19) {
            VStack(alignment: .leading, spacing: 5) {
                Text("Ajouter un compte").font(.system(size: 20, weight: .semibold, design: .rounded))
                Text("Donne-lui un nom pour le reconnaître facilement.").font(.system(size: 12)).foregroundStyle(.secondary)
            }
            VStack(alignment: .leading, spacing: 7) {
                Text("NOM DU COMPTE").font(.system(size: 10, weight: .bold)).tracking(0.8).foregroundStyle(.secondary)
                TextField("Ex. Personnel, Travail, Client…", text: $name)
                    .textFieldStyle(.roundedBorder).onSubmit(create)
            }
            VStack(alignment: .leading, spacing: 9) {
                Text("COULEUR").font(.system(size: 10, weight: .bold)).tracking(0.8).foregroundStyle(.secondary)
                HStack(spacing: 10) {
                    ForEach(ClaudeProfile.colors, id: \.self) { option in
                        Button { color = option } label: {
                            Circle().fill(Color(hex: option)).frame(width: 22, height: 22)
                                .overlay { if color == option { Image(systemName: "checkmark").font(.system(size: 10, weight: .bold)).foregroundStyle(.white) } }
                                .padding(3).overlay(Circle().stroke(color == option ? Color.primary.opacity(0.65) : .clear, lineWidth: 1))
                        }
                        .buttonStyle(.plain)
                    }
                }
            }
            HStack {
                Spacer()
                Button("Annuler", role: .cancel) { dismiss() }.keyboardShortcut(.cancelAction)
                Button("Créer le compte", action: create).buttonStyle(.borderedProminent)
                    .tint(Color(red: 0.78, green: 0.37, blue: 0.27)).disabled(name.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
                    .keyboardShortcut(.defaultAction)
            }
        }
        .padding(24).frame(width: 410)
    }

    private func create() { onCreate(name, color); dismiss() }
}

struct RenameProfileSheet: View {
    @Environment(\.dismiss) private var dismiss
    @State private var name: String
    let onSave: (String) -> Void

    init(name: String, onSave: @escaping (String) -> Void) {
        _name = State(initialValue: name)
        self.onSave = onSave
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            Text("Renommer le compte").font(.system(size: 19, weight: .semibold, design: .rounded))
            TextField("Nom du compte", text: $name).textFieldStyle(.roundedBorder).onSubmit(save)
            HStack {
                Spacer()
                Button("Annuler", role: .cancel) { dismiss() }.keyboardShortcut(.cancelAction)
                Button("Enregistrer", action: save).buttonStyle(.borderedProminent)
                    .disabled(name.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty).keyboardShortcut(.defaultAction)
            }
        }
        .padding(24).frame(width: 360)
    }

    private func save() { onSave(name); dismiss() }
}

extension Color {
    init(hex: String) {
        let value = UInt64(hex.dropFirst(), radix: 16) ?? 0xD97757
        self.init(.sRGB,
                  red: Double((value >> 16) & 0xff) / 255,
                  green: Double((value >> 8) & 0xff) / 255,
                  blue: Double(value & 0xff) / 255,
                  opacity: 1)
    }
}
