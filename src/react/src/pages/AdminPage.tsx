import {
    Spinner,
    makeStyles,
    tokens,
    TabList,
    Tab
} from "@fluentui/react-components";
import { useState } from "react";
import { useAdminDocuments } from "../hooks/useAdminDocuments";
import { useSettings } from "../hooks/useSettings";
import { DocumentsTab } from "../components/admin/DocumentsTab";
import { DocumentChunksTab } from "../components/admin/DocumentChunksTab";
import { SettingsTab } from "../components/admin/SettingsTab";
import { PredefinedPromptsTab } from "../components/admin/PredefinedPromptsTab";
import { PredefinedPrompt } from "../models/Settings";
import { useLayoutStyles } from "../styles/sharedStyles";

const useStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        marginLeft: tokens.spacingVerticalM,
        marginRight: tokens.spacingVerticalM,
        height: '100vh',
        overflow: 'hidden',
    },
    header: {
        display: 'flex',
        flexDirection: 'column',
        gap: '10px'
    },
    tabContent: {
        marginTop: tokens.spacingVerticalM,
        minWidth: '800px',
        flex: '1 1 auto',
        overflow: 'hidden',
    },
    tabContainer: {
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        overflow: 'hidden',
    },
    contentScroll: {
        flex: '1 1 auto',
        padding: tokens.spacingVerticalXS,
    }
});

export const AdminPage = () => {
    const styles = useStyles();
    const layoutStyles = useLayoutStyles();
    const [selectedTab, setSelectedTab] = useState<string>("documents");
    
    const {
        allDocuments,
        documentChunks,
        isLoadingDocuments,
        isLoadingChunks,
        handleSearch,
        documentId,
        setDocumentId
    } = useAdminDocuments();

    //Settings state and handlers
    const { settings, updateSettings, isLoading: isLoadingSettings } = useSettings();
    
    const handleSettingsUpdate = (newSettings: { 
        temperature: number; 
        seed: number;
        allowFollowUpPrompts: boolean;
        allowInitialPromptRewrite: boolean;
        allowInitialPromptToHelpUser: boolean;
        useSemanticRanker: boolean;
    }) => {
        if (settings) {
            const updatedSettings = {
                ...settings,
                predefinedPrompts: settings.predefinedPrompts,
                temperature: newSettings.temperature,
                seed: newSettings.seed,
                allowFollowUpPrompts: newSettings.allowFollowUpPrompts,
                allowInitialPromptRewrite: newSettings.allowInitialPromptRewrite,
                allowInitialPromptToHelpUser: newSettings.allowInitialPromptToHelpUser,
                useSemanticRanker: newSettings.useSemanticRanker
            };
            console.log("Updating settings:", updatedSettings);
            updateSettings(updatedSettings);
        }
    };

    const handlePredefinePromptsUpdate = (prompts: PredefinedPrompt[]) => {
        // Only update predefinedPrompts, preserve all other settings
        if (settings) {
            const updatedSettings = {
                ...settings,
                predefinedPrompts: prompts
            };
            console.log("Updating predefined prompts:", updatedSettings);
            updateSettings(updatedSettings);
        }
    };

    if (isLoadingDocuments && selectedTab === "documents") {
        return <Spinner />;
    }

    return (
        <div className={`${styles.container} ${layoutStyles.scrollContainer}`}>
            <div className={styles.header}>
                <h1>Admin Dashboard</h1>
                
                <TabList selectedValue={selectedTab} onTabSelect={(_, data) => setSelectedTab(data.value as string)}>
                    <Tab value="documents">Documents</Tab>
                    <Tab value="chunks">Document Chunks</Tab>
                    <Tab value="settings">Settings</Tab>
                    <Tab value="prompts">Predefined Prompts</Tab>
                </TabList>
            </div>

            <div className={styles.tabContent}>
                {selectedTab === "documents" && (
                    <DocumentsTab 
                        documents={allDocuments} 
                        isLoading={isLoadingDocuments} 
                    />
                )}

                {selectedTab === "chunks" && (
                    <DocumentChunksTab
                        documentId={documentId}
                        setDocumentId={setDocumentId}
                        documentChunks={documentChunks}
                        isLoading={isLoadingChunks}
                        onSearch={handleSearch}
                    />
                )}

                {selectedTab === "settings" && (
                    <SettingsTab 
                        isLoading={isLoadingSettings || false}
                        settings={settings}
                        onSaveSettings={handleSettingsUpdate}
                    />
                )}

                {selectedTab === "prompts" && (
                    <PredefinedPromptsTab
                        isLoading={isLoadingSettings || false}
                        prompts={settings?.predefinedPrompts || []}
                        onSavePrompts={handlePredefinePromptsUpdate}
                    />
                )}
            </div>
        </div>
    );
};